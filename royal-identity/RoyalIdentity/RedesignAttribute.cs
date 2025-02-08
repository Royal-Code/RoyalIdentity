namespace RoyalIdentity;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public class RedesignAttribute(string information) : Attribute { }
