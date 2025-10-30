using PriceNegotiationApp.Domain.Models.Abstract;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Linq;

namespace PriceNegotiationApp.Domain.Models.Products
{
	public class Product : Entity<int>
	{
		public string Name { get; private set; }
		public ProductPrice Price { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
		/// <summary>
		/// Empty constructor for EF.
		/// </summary>
		private Product() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

		public Product(string name, ProductPrice price)
		{
			Name = name;
			Price = price;
		}

		public void Update(string name, ProductPrice price)
		{
			Name = name;
			Price = price;
		}
    }
}
