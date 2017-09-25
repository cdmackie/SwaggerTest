using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace SwaggerTest
{
	public class MockHostingEnvironment : IHostingEnvironment
	{
		public string ApplicationName { get; set; }

		public IFileProvider ContentRootFileProvider { get {throw new NotImplementedException();} set {throw new NotImplementedException();} }

		public string ContentRootPath { get; set; }

		public string EnvironmentName { get; set; }

		public IFileProvider WebRootFileProvider { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

		public string WebRootPath { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

		public MockHostingEnvironment(string path)
		{
			ContentRootPath = path;
			ApplicationName = "SwaggerTest";
			EnvironmentName = "Development";
		}
	}
}
