namespace RoyalIdentity;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RedesignAttribute(string information) : Attribute { }
