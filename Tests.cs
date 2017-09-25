using System;
using System.Collections.Generic;
using System.Text;

using Xunit;

using ServiceStack.Testing;

namespace SwaggerTest
{
  public class Tests : IClassFixture<ServiceFixture>
  {
    public TestService Service { get; set; }

    public Tests(ServiceFixture fixture)
    {
      Service = fixture.TryResolve<TestService>();
      Service.Request = new MockHttpRequest();
    }

    [Fact]
    public void Test()
    {
      var response = Service.ExecuteRequest(new TestRequest { Id = 1 });
      Assert.NotNull(response);
    }
  }
}
