namespace RoyalIdentity;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
internal class RedesignAttribute(string information) : Attribute { }
