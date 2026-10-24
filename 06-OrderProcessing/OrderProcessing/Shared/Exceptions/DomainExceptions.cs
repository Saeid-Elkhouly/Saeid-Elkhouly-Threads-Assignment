namespace OrderProcessingSystem.Shared.Exceptions;

public sealed class CustomerNotFoundException : Exception { public CustomerNotFoundException(string message) : base(message) { } }
public sealed class InvalidCouponException : Exception { public InvalidCouponException(string message) : base(message) { } }
public sealed class ShippingProviderUnavailableException : Exception { public ShippingProviderUnavailableException(string message) : base(message) { } }
public sealed class DatabaseWriteException : Exception { public DatabaseWriteException(string message) : base(message) { } }
public sealed class SmtpUnavailableException : Exception { public SmtpUnavailableException(string message) : base(message) { } }
