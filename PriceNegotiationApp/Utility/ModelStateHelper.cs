using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PriceNegotiationApp.Utility.Utility
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
					e.Value.Errors.First().Exception
				}).Cast<object>().ToList();

			return errors;
		}
	}
}
