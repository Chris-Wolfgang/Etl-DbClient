// Static pool of hand-shaped test types the fuzz properties dispatch to
// via CsCheck's Int-index generator. Each represents a distinct shape
// class of record that DbCommandBuilder must handle:
//
//   SingleColumn         one column, no Key           → Update throws
//   IdentityKeyOnly      identity Key, no other cols  → Update = "UPDATE t SET ... WHERE id = @Id" degenerate
//   IdentityKeyWithColumns  standard shape             → Update = full
//   CompositeKey         two-Key WHERE                → covers AND join
//   AllKey               every column is Key          → Update SET is empty (throws)
//   WithNotMapped        [NotMapped] column present   → skipped from SQL
//   MixedCase            case-varied column names     → OrdinalIgnoreCase lookup

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Wolfgang.Etl.DbClient.Tests.Fuzz;

internal static class Shape
{
    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("single")]
    public sealed class SingleColumn
    {
        [Column("value")] public string Value { get; set; } = "";
    }

    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("identity_only")]
    public sealed class IdentityKeyOnly
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("payload")]
        public string Payload { get; set; } = "";
    }

    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("standard")]
    public sealed class IdentityKeyWithColumns
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = "";

        [Column("value")]
        public decimal Value { get; set; }

        [Column("created_utc")]
        public DateTime CreatedUtc { get; set; }
    }

    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("composite")]
    public sealed class CompositeKey
    {
        [Key]
        [Column("outer_id")]
        public int OuterId { get; set; }

        [Key]
        [Column("inner_id")]
        public int InnerId { get; set; }

        [Column("payload")]
        public string Payload { get; set; } = "";
    }

    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("all_key")]
    public sealed class AllKey
    {
        [Key]
        [Column("a")]
        public int A { get; set; }

        [Key]
        [Column("b")]
        public int B { get; set; }
    }

    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("notmapped")]
    public sealed class WithNotMapped
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = "";

        [NotMapped]
        public string DisplayName { get; set; } = "";
    }

    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("mixed_case")]
    public sealed class MixedCase
    {
        [Key]
        [Column("PK_ID")]
        public int PkId { get; set; }

        [Column("Field_Name")]
        public string FieldName { get; set; } = "";

        [Column("field_value")]
        public string FieldValue { get; set; } = "";
    }
}
