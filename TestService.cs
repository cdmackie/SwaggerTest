using ServiceStack;

namespace SwaggerTest
{
  [Route("/test/{Id}")]
  public class TestRequest : IReturn<TestResponse>
  {
    public int Id { get; set; }
  }
  public class TestResponse
  {
    public int Id { get; set; }
  }

  public class TestService : Service, IService
  {
    public object Any(TestRequest request)
    {
      return new TestResponse() { Id = request.Id };
    }
  }
}