namespace VMAzureApp.Models;

/// <summary>
/// Matches a virtual machine against a free-text search term. A VM matches when
/// the term is empty, or appears (case-insensitively) in its name, resource
/// group, region, size, OS type, or current status.
/// </summary>
public static class VmSearchFilter
{
    public static bool Matches(VirtualMachineInfo? virtualMachine, string? search)
    {
        if (virtualMachine is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        string term = search.Trim();

        return Contains(virtualMachine.Name, term)
            || Contains(virtualMachine.ResourceGroupName, term)
            || Contains(virtualMachine.Location, term)
            || Contains(virtualMachine.Size, term)
            || Contains(virtualMachine.OsType, term)
            || Contains(virtualMachine.PowerStateDisplay, term);
    }

    private static bool Contains(string? value, string term)
    {
        return value is not null && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }
}
