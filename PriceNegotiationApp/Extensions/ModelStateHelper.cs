using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PriceNegotiationApp.Extensions
{
	public static class ModelStateHelper
	{
		public static List<object> GetErrors(ModelStateDictionary modelState)
		{
			var errors = modelState.Where(e => e.Value.Errors.Count > 0)
				.Select(e => new
				{
					Name = e.Key,
					Message = e.Value.Errors.First().ErrorMessage,
					Exception = e.Value.Errors.First().Exception
				}).Cast<object>().ToList();

			return errors;
		}
	}
}
