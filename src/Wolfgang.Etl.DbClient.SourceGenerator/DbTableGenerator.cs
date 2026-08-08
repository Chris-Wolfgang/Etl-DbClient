using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wolfgang.Etl.DbClient.SourceGenerator;

/// <summary>
/// Walks every <c>partial class</c> / <c>partial record</c> decorated with
/// <c>[DbTable("name")]</c> and emits a partial declaration adding:
///
/// <list type="bullet">
///   <item><description><c>public const string Insert</c> — the canonical
///         <c>INSERT</c> SQL for the table</description></item>
///   <item><description><c>public const string Select</c> — a
///         <c>SELECT</c> covering every mapped column, aliasing
///         <c>col AS Property</c> when the column and property names
///         differ (matching runtime <c>DbCommandBuilder.BuildSelect</c>)
///         </description></item>
///   <item><description><c>public const string Update</c> — an
///         <c>UPDATE … SET … WHERE …</c> whose SET covers every non-key
///         column and whose WHERE covers every <c>[DbKey]</c> property.
///         Emitted only when the type has at least one <c>[DbKey]</c>
///         property AND at least one non-key mapped column.</description></item>
///   <item><description><c>public const string Delete</c> — a
///         <c>DELETE FROM … WHERE …</c> whose WHERE covers every
///         <c>[DbKey]</c> property. Emitted only when the type has at
///         least one <c>[DbKey]</c> property.</description></item>
///   <item><description><c>public static void Bind(Dapper.DynamicParameters
///         parameters, T record)</c> — reflection-free binder</description></item>
/// </list>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DbTableGenerator : IIncrementalGenerator
{
    private const string DbTableAttributeFullName = "Wolfgang.Etl.DbClient.DbTableAttribute";
    private const string DbColumnAttributeFullName = "Wolfgang.Etl.DbClient.DbColumnAttribute";
    private const string DbKeyAttributeFullName = "Wolfgang.Etl.DbClient.DbKeyAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .ForAttributeWithMetadataName
            (
                DbTableAttributeFullName,
                static (node, _) => node is TypeDeclarationSyntax t && t.Modifiers.Any(m => m.ValueText == "partial"),
                static (ctx, _) => Extract(ctx)
            )
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        context.RegisterSourceOutput(candidates, Emit);
    }



    private static TableModel? Extract(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type)
        {
            return null;
        }

        var attr = ctx.Attributes.FirstOrDefault();
        if (attr is null || attr.ConstructorArguments.Length == 0)
        {
            return null;
        }

        var tableName = attr.ConstructorArguments[0].Value as string;
        if (string.IsNullOrEmpty(tableName))
        {
            return null;
        }

        var columns = new List<ColumnModel>();
        foreach (var member in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic || member.IsIndexer)
            {
                continue;
            }

            var columnAttr = member.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == DbColumnAttributeFullName);

            var skip = false;
            var columnName = member.Name;
            if (columnAttr is not null)
            {
                if (columnAttr.ConstructorArguments.Length > 0 && columnAttr.ConstructorArguments[0].Value is string colArg && !string.IsNullOrEmpty(colArg))
                {
                    columnName = colArg;
                }
                foreach (var named in columnAttr.NamedArguments)
                {
                    if (named.Key == "Skip" && named.Value.Value is bool b)
                    {
                        skip = b;
                    }
                }
            }

            if (skip)
            {
                continue;
            }

            var isKey = member.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == DbKeyAttributeFullName);

            columns.Add(new ColumnModel(member.Name, columnName, isKey));
        }

        if (columns.Count == 0)
        {
            return null;
        }

        return new TableModel
        (
            type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            type.Name,
            type.TypeKind == TypeKind.Struct ? "struct" : (type.IsRecord ? "record" : "class"),
            tableName!,
            columns.ToImmutableArray()
        );
    }



    private static void Emit(SourceProductionContext ctx, TableModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        if (model.Namespace is not null)
        {
            sb.Append("namespace ").Append(model.Namespace).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append("partial ").Append(model.Kind).Append(' ').Append(model.TypeName).AppendLine();
        sb.AppendLine("{");

        // Insert SQL constant.
        var colList = string.Join(", ", model.Columns.Select(c => c.ColumnName));
        var paramList = string.Join(", ", model.Columns.Select(c => "@" + c.PropertyName));
        sb.Append("    public const string Insert = \"INSERT INTO ")
          .Append(model.TableName)
          .Append(" (").Append(colList).Append(") VALUES (").Append(paramList).AppendLine(")\";");
        sb.AppendLine();

        // Select SQL constant. Alias `col AS Property` only when the
        // column and property names differ — the runtime
        // DbCommandBuilder.BuildSelect follows the same rule, so the
        // generated + reflection paths produce identical strings and
        // consumers can cut over to the generator without a wire-level
        // behaviour change.
        var selectList = string.Join
        (
            ", ",
            model.Columns.Select(c => string.Equals(c.ColumnName, c.PropertyName, System.StringComparison.Ordinal)
                ? c.ColumnName
                : c.ColumnName + " AS " + c.PropertyName)
        );
        sb.Append("    public const string Select = \"SELECT ")
          .Append(selectList)
          .Append(" FROM ")
          .Append(model.TableName)
          .AppendLine("\";");
        sb.AppendLine();

        // Update / Delete SQL constants — emitted only when the type has
        // at least one [DbKey] property. Update additionally needs at
        // least one non-key column to have anything to SET.
        var keyColumns = model.Columns.Where(c => c.IsKey).ToList();
        var setColumns = model.Columns.Where(c => !c.IsKey).ToList();

        if (keyColumns.Count > 0)
        {
            var whereClause = string.Join
            (
                " AND ",
                keyColumns.Select(c => c.ColumnName + " = @" + c.PropertyName)
            );

            if (setColumns.Count > 0)
            {
                var setClause = string.Join
                (
                    ", ",
                    setColumns.Select(c => c.ColumnName + " = @" + c.PropertyName)
                );

                sb.Append("    public const string Update = \"UPDATE ")
                  .Append(model.TableName)
                  .Append(" SET ").Append(setClause)
                  .Append(" WHERE ").Append(whereClause)
                  .AppendLine("\";");
                sb.AppendLine();
            }

            sb.Append("    public const string Delete = \"DELETE FROM ")
              .Append(model.TableName)
              .Append(" WHERE ").Append(whereClause)
              .AppendLine("\";");
            sb.AppendLine();
        }

        // Bind helper — reflection-free DynamicParameters population.
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Reflection-free Dapper parameter binder generated for this record.");
        sb.AppendLine("    /// </summary>");
        sb.Append("    public static void Bind(global::Dapper.DynamicParameters parameters, ").Append(model.TypeName).AppendLine(" record)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (parameters is null) throw new global::System.ArgumentNullException(nameof(parameters));");
        foreach (var col in model.Columns)
        {
            sb.Append("        parameters.Add(\"@").Append(col.PropertyName).Append("\", record.").Append(col.PropertyName).AppendLine(");");
        }
        sb.AppendLine("    }");

        sb.AppendLine("}");

        ctx.AddSource(model.TypeName + ".DbTable.g.cs", sb.ToString());
    }



    private sealed record TableModel
    (
        string? Namespace,
        string TypeName,
        string Kind,
        string TableName,
        ImmutableArray<ColumnModel> Columns
    );



    private sealed record ColumnModel(string PropertyName, string ColumnName, bool IsKey);
}
