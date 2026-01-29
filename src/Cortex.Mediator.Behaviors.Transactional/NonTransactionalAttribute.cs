using System;

namespace Cortex.Mediator.Behaviors.Transactional
{
    /// <summary>
    /// Attribute to mark commands that should be excluded from transactional behavior.
    /// Commands decorated with this attribute will execute without a transaction wrapper.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class NonTransactionalAttribute : Attribute
    {
    }
}
