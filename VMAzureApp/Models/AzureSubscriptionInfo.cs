namespace VMAzureApp.Models;

public sealed class AzureSubscriptionInfo
{
    public AzureSubscriptionInfo(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }

    public string Name { get; }
}
