using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using PriceNegotiationApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceNegotiationApp.Tests.Unit_Tests
{
	public static class DbContextProvider
	{
        public static AppDbContext GetInMemoryDbContext(IWebHostEnvironment environment = null)
        {
            environment ??= CreateDefaultDevelopmentEnvironment();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("Tests");

            if (environment.IsDevelopment())
            {
                optionsBuilder.EnableSensitiveDataLogging();
				optionsBuilder.EnableDetailedErrors();
			}

            return new AppDbContext(optionsBuilder.Options, environment);
        }

		private static IWebHostEnvironment CreateDefaultDevelopmentEnvironment()
		{
			var substituteEnvironment = Substitute.For<IWebHostEnvironment>();
			substituteEnvironment.EnvironmentName.Returns("Development");
			return substituteEnvironment;
		}
	}
}
