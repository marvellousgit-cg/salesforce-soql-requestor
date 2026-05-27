using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SalesforceClient.Tests")]
// Required so Moq's Castle DynamicProxy can generate mocks of internal interfaces in tests.
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
