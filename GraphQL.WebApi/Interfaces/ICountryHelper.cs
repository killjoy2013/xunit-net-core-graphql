using GraphQL.WebApi.Models;

namespace GraphQL.WebApi.Interfaces
{
    public interface ICountryHelper
    {
        Country CreateCountry(string countryName);
        Country QueryCountry(string countryName);
    }
}
