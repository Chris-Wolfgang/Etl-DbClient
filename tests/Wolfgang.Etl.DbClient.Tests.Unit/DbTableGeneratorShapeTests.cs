using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Dapper;
using JetBrains.Annotations;
using Xunit;

// These fixtures are compiled with the generator: each shape below used to produce code that did
// not compile (or collided), so merely building this file is part of the test. The assertions then
// check the emitted SQL and binder.

namespace Wolfgang.Etl.DbClient.Tests.Unit
{
    public sealed partial class GeneratorOuter
    {
        [ExcludeFromCodeCoverage]
        [DbTable("nested_rows")]
        [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
        public partial record NestedRow
        {
            public int Id { get; init; }

            public string Label { get; init; } = string.Empty;
        }
    }



    [ExcludeFromCodeCoverage]
    [DbTable("generic_rows")]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public partial record GeneratorGenericRow<T>
        where T : notnull
    {
        public int Id { get; init; }

        public T? Payload { get; init; }
    }



    [ExcludeFromCodeCoverage]
    [DbTable("struct_rows")]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public partial record struct GeneratorStructRow
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }



    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public record GeneratorBaseRow
    {
        public int BaseId { get; init; }
    }



    [ExcludeFromCodeCoverage]
    [DbTable("derived_rows")]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public partial record GeneratorDerivedRow : GeneratorBaseRow
    {
        public string Extra { get; init; } = string.Empty;
    }



    [ExcludeFromCodeCoverage]
    [DbTable("quoted \"table\"")]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public partial record GeneratorQuotedRow
    {
        [DbColumn("back\\slash \"col\"")]
        public int Value { get; init; }

        public string @class { get; init; } = string.Empty;
    }
}



namespace Wolfgang.Etl.DbClient.Tests.Unit.ShapeA
{
    [ExcludeFromCodeCoverage]
    [DbTable("duplicate_a")]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public partial record GeneratorDuplicate
    {
        public int Id { get; init; }
    }
}



namespace Wolfgang.Etl.DbClient.Tests.Unit.ShapeB
{
    [ExcludeFromCodeCoverage]
    [DbTable("duplicate_b")]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public partial record GeneratorDuplicate
    {
        public int Id { get; init; }
    }
}



namespace Wolfgang.Etl.DbClient.Tests.Unit
{
    public class DbTableGeneratorShapeTests
    {
        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

        private static string? Sql<T>(string constName)
            => typeof(T).GetField(constName, PublicStatic)?.GetRawConstantValue() as string;



        [Fact]
        public void Generator_extends_a_nested_type_in_place()
        {
            Assert.Equal
            (
                "INSERT INTO nested_rows (Id, Label) VALUES (@Id, @Label)",
                Sql<GeneratorOuter.NestedRow>("Insert")
            );
        }



        [Fact]
        public void Generator_repeats_the_type_parameters_of_a_generic_type()
        {
            Assert.Equal
            (
                "INSERT INTO generic_rows (Id, Payload) VALUES (@Id, @Payload)",
                Sql<GeneratorGenericRow<string>>("Insert")
            );

            var parameters = new DynamicParameters();
            typeof(GeneratorGenericRow<string>).GetMethod("Bind", PublicStatic)!
                .Invoke(null, [parameters, new GeneratorGenericRow<string> { Id = 3, Payload = "p" }]);

            Assert.Equal(3, parameters.Get<int>("@Id"));
        }



        [Fact]
        public void Generator_emits_a_record_struct_as_a_record_struct()
        {
            Assert.Equal
            (
                "SELECT Id, Name FROM struct_rows",
                Sql<GeneratorStructRow>("Select")
            );
        }



        [Fact]
        public void Generator_gives_same_named_types_in_different_namespaces_their_own_output()
        {
            Assert.Equal("INSERT INTO duplicate_a (Id) VALUES (@Id)", Sql<ShapeA.GeneratorDuplicate>("Insert"));
            Assert.Equal("INSERT INTO duplicate_b (Id) VALUES (@Id)", Sql<ShapeB.GeneratorDuplicate>("Insert"));
        }



        [Fact]
        public void Generator_includes_inherited_properties_like_the_reflection_builder()
        {
            Assert.Equal
            (
                "INSERT INTO derived_rows (Extra, BaseId) VALUES (@Extra, @BaseId)",
                Sql<GeneratorDerivedRow>("Insert")
            );
        }



        [Fact]
        public void Generator_escapes_names_and_keyword_identifiers()
        {
            Assert.Equal
            (
                "INSERT INTO quoted \"table\" (back\\slash \"col\", class) VALUES (@Value, @class)",
                Sql<GeneratorQuotedRow>("Insert")
            );

            var parameters = new DynamicParameters();
            typeof(GeneratorQuotedRow).GetMethod("Bind", PublicStatic)!
                .Invoke(null, [parameters, new GeneratorQuotedRow { Value = 5, @class = "c" }]);

            Assert.Equal("c", parameters.Get<string>("@class"));
        }
    }
}
