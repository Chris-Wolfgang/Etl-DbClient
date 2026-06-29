using JetBrains.Annotations;

namespace Wolfgang.Etl.DbClient.Example;

[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
public class HighEarnerRecord
{
    public string FullName { get; set; } = string.Empty;
    public double Salary { get; set; }
}
