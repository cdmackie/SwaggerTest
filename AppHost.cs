using Funq;
using System;
using System.Collections.Generic;

using ServiceStack;
using ServiceStack.Testing;
using ServiceStack.Host;
using ServiceStack.Web;
using ServiceStack.Api.Swagger;
using ServiceStack.Text;

namespace SwaggerTest
{
  public class AppHost : BasicAppHost
  {
    public AppHost() : base(typeof(AppHost).GetAssembly())
    {
    }

    public override void Configure(Container container)
    {
      container.RegisterAutoWired<TestService>();
    }

    public override IServiceRunner<TRequest> CreateServiceRunner<TRequest>(ActionContext actionContext)
    {
      return new CacheServiceRunner<TRequest>(this, actionContext); //Cached per Service Action
    }
  }

  public class CacheServiceRunner<T> : ServiceRunner<T>
  {
    public CacheServiceRunner(IAppHost appHost, ActionContext actionContent)
      : base(appHost, actionContent)
    {
    }

    private object ExecuteAsync(IRequest requestContext, object instance, T request)
    {
      return base.Execute(requestContext, instance, request);
    }

    public override object OnAfterExecute(IRequest requestContext, object response)
    {
      response = base.OnAfterExecute(requestContext, response);

      var test = new HttpResult(new SwaggerResourcesResponse
      {
        BasePath = requestContext.GetBaseUrl(),
        Apis = new List<SwaggerResourceRef>(),
        ApiVersion = HostContext.Config.ApiVersion,
        Info = new SwaggerInfo
        {
          Title = HostContext.ServiceName,
        }
      })
      {
        ResultScope = () => JsConfig.With(includeNullValues: false)
      };
      requestContext.ToOptimizedResult(test);

      return response;
    }
  }

  public class ServiceFixture
  {
    public IAppHost AppHost { get; set; }

    public ServiceFixture()
    {
      AppHost = new AppHost().Init();
    }

    public T TryResolve<T>()
    {
      return AppHost.TryResolve<T>();
    }
  }
}
