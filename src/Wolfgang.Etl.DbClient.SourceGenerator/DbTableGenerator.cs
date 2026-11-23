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
        var seenProperties = new HashSet<string>();
        foreach (var member in EnumerateProperties(type))
        {
            // An override or a `new` redeclaration hides the base property of the same name; the
            // runtime reflection path (GetProperties) lists each name once, so do the same.
            if (!seenProperties.Add(member.Name))
            {
                continue;
            }

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

        // Containing types (outermost first) are re-declared as partial so a nested type extends
        // X.Outer.Inner instead of declaring a new top-level X.Inner.
        var containers = new List<string>();
        for (var container = type.ContainingType; container is not null; container = container.ContainingType)
        {
            containers.Insert(0, "partial " + KindKeyword(container) + " " + NameWithTypeParameters(container));
        }

        return new TableModel
        (
            type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            KindKeyword(type),
            NameWithTypeParameters(type),
            string.Join("|", containers),
            ComputeHintName(type),
            tableName!,
            columns.ToImmutableArray()
        );
    }



    // The type's own properties first, then each base type's, nearest base first - the order
    // Type.GetProperties(Public | Instance) reports them at run time.
    private static IEnumerable<IPropertySymbol> EnumerateProperties(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                yield return property;
            }
        }
    }



    private static string KindKeyword(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Struct)
        {
            return type.IsRecord ? "record struct" : "struct";
        }

        return type.IsRecord ? "record" : "class";
    }



    // `Row<T, U>` for a generic type, `Row` otherwise: a partial declaration must repeat the type
    // parameters or it declares a different type.
    private static string NameWithTypeParameters(INamedTypeSymbol type)
    {
        return type.TypeParameters.Length == 0
            ? type.Name
            : type.Name + "<" + string.Join(", ", type.TypeParameters.Select(static p => p.Name)) + ">";
    }



    // Unique per type: the containing types, namespace and arity are part of the name, so two
    // [DbTable] types with the same simple name no longer collide on AddSource's hint name.
    private static string ComputeHintName(INamedTypeSymbol type)
    {
        var parts = new List<string>();
        for (ISymbol? current = type; current is not null; current = current.ContainingSymbol)
        {
            if (current is INamespaceSymbol ns)
            {
                if (ns.IsGlobalNamespace)
                {
                    break;
                }

                parts.Insert(0, ns.Name);
            }
            else
            {
                parts.Insert(0, current.MetadataName);
            }
        }

        var builder = new StringBuilder();
        foreach (var ch in string.Join(".", parts))
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch == '.' ? ch : '_');
        }

        return builder.ToString();
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

        var containers = model.Containers.Length == 0 ? System.Array.Empty<string>() : model.Containers.Split('|');
        foreach (var container in containers)
        {
            sb.Append(container).AppendLine();
            sb.AppendLine("{");
        }

        sb.Append("partial ").Append(model.Kind).Append(' ').Append(model.TypeDeclaration).AppendLine();
        sb.AppendLine("{");

        // Insert SQL constant.
        var colList = string.Join(", ", model.Columns.Select(c => c.ColumnName));
        var paramList = string.Join(", ", model.Columns.Select(c => "@" + c.PropertyName));
        AppendConst(sb, "Insert", "INSERT INTO " + model.TableName + " (" + colList + ") VALUES (" + paramList + ")");
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
        AppendConst(sb, "Select", "SELECT " + selectList + " FROM " + model.TableName);
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

                AppendConst(sb, "Update", "UPDATE " + model.TableName + " SET " + setClause + " WHERE " + whereClause);
                sb.AppendLine();
            }

            AppendConst(sb, "Delete", "DELETE FROM " + model.TableName + " WHERE " + whereClause);
            sb.AppendLine();
        }

        // Bind helper — reflection-free DynamicParameters population.
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Reflection-free Dapper parameter binder generated for this record.");
        sb.AppendLine("    /// </summary>");
        sb.Append("    public static void Bind(global::Dapper.DynamicParameters parameters, ").Append(model.TypeDeclaration).AppendLine(" record)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (parameters is null) throw new global::System.ArgumentNullException(nameof(parameters));");
        foreach (var col in model.Columns)
        {
            // The parameter name is a string; the member access is an identifier, so a property named
            // like a keyword (`class`) needs the verbatim `@` prefix there.
            sb.Append("        parameters.Add(")
              .Append(Literal("@" + col.PropertyName))
              .Append(", record.").Append(Identifier(col.PropertyName)).AppendLine(");");
        }
        sb.AppendLine("    }");

        sb.AppendLine("}");
        foreach (var _ in containers)
        {
            sb.AppendLine("}");
        }

        ctx.AddSource(model.HintName + ".DbTable.g.cs", sb.ToString());
    }



    // Table and column names are caller text: escape them as a C# literal so a quote or backslash
    // (a quoted identifier, for example) cannot break the generated file.
    private static void AppendConst(StringBuilder sb, string name, string value)
    {
        sb.Append("    public const string ").Append(name).Append(" = ").Append(Literal(value)).AppendLine(";");
    }



    private static string Literal(string value) => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, quote: true);



    private static string Identifier(string name)
    {
        var isKeyword = Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None;
        return isKeyword ? "@" + name : name;
    }



    private sealed record TableModel
    (
        string? Namespace,
        string Kind,
        string TypeDeclaration,
        string Containers,
        string HintName,
        string TableName,
        ImmutableArray<ColumnModel> Columns
    );



    private sealed record ColumnModel(string PropertyName, string ColumnName, bool IsKey);
}
