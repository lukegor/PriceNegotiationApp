using Microsoft.EntityFrameworkCore;
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
		public static AppDbContext GetInMemoryDbContext() =>
			new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
				.UseInMemoryDatabase("Tests").Options);
	}
}
