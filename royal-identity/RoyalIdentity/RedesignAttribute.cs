namespace RoyalIdentity;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = true)]
public class RedesignAttribute(string information) : Attribute { }
