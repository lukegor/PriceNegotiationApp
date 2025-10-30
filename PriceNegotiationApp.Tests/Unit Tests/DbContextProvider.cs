using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using PriceNegotiationApp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PriceNegotiationApp.Tests.Unit_Tests
{
    public static class DbContextProvider
	{
        public static AppDbContext GetInMemoryDbContext()
        {
            string dbName = $"TestDb_{Guid.NewGuid()}";

            var environment = CreateDefaultDevelopmentEnvironment();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName);

            if (environment.IsDevelopment())
            {
                optionsBuilder.EnableSensitiveDataLogging();
				optionsBuilder.EnableDetailedErrors();
			}

            return new AppDbContext(optionsBuilder.Options);
        }

		private static IWebHostEnvironment CreateDefaultDevelopmentEnvironment()
		{
			var substituteEnvironment = Substitute.For<IWebHostEnvironment>();
			substituteEnvironment.EnvironmentName.Returns("Development");
			return substituteEnvironment;
		}
	}
}
