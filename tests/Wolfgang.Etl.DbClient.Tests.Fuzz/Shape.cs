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
//
// The shapes are abstract with abstract properties: DbCommandBuilder only
// ever reads their metadata through typeof(...), so no instance is created
// and there are no accessor bodies that would sit unexecuted.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JetBrains.Annotations;

namespace Wolfgang.Etl.DbClient.Tests.Fuzz;

internal static class Shape
{
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("single")]
    public abstract class SingleColumn
    {
        [Column("value")] public abstract string Value { get; set; }
    }

    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("identity_only")]
    public abstract class IdentityKeyOnly
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public abstract int Id { get; set; }

        [Column("payload")]
        public abstract string Payload { get; set; }
    }

    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("standard")]
    public abstract class IdentityKeyWithColumns
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public abstract int Id { get; set; }

        [Column("name")]
        public abstract string Name { get; set; }

        [Column("value")]
        public abstract decimal Value { get; set; }

        [Column("created_utc")]
        public abstract DateTime CreatedUtc { get; set; }
    }

    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("composite")]
    public abstract class CompositeKey
    {
        [Key]
        [Column("outer_id")]
        public abstract int OuterId { get; set; }

        [Key]
        [Column("inner_id")]
        public abstract int InnerId { get; set; }

        [Column("payload")]
        public abstract string Payload { get; set; }
    }

    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("all_key")]
    public abstract class AllKey
    {
        [Key]
        [Column("a")]
        public abstract int A { get; set; }

        [Key]
        [Column("b")]
        public abstract int B { get; set; }
    }

    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("notmapped")]
    public abstract class WithNotMapped
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public abstract int Id { get; set; }

        [Column("name")]
        public abstract string Name { get; set; }

        [NotMapped]
        public abstract string DisplayName { get; set; }
    }

    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("mixed_case")]
    public abstract class MixedCase
    {
        [Key]
        [Column("PK_ID")]
        public abstract int PkId { get; set; }

        [Column("Field_Name")]
        public abstract string FieldName { get; set; }

        [Column("field_value")]
        public abstract string FieldValue { get; set; }
    }
}
