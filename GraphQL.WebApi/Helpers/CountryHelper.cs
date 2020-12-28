using GraphQL.WebApi.Interfaces;
using GraphQL.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace GraphQL.WebApi.Helpers
{
    public class CountryHelper: ICountryHelper
    {   
        private readonly IGenericRepository<Country> _countryRepository;
        public CountryHelper(IGenericRepository<Country> countryRepository)
        {
            _countryRepository = countryRepository;                
        }


        public Country CreateCountry(string countryName)
        {
            var newCountry = new Country
            {
                name = countryName
            };
            Country createdCountry = null;
            try
            {
                createdCountry = _countryRepository.Insert(newCountry);
            }
            catch (DbUpdateException dbException)
            {
                if (dbException.InnerException.Message.Contains("duplicate key value"))
                {
                    throw new ExecutionError("duplicate_country_not_allowed", dbException.InnerException);
                }
                else
                {
                    throw dbException;
                }
            }
            return createdCountry;
        }
        public Country QueryCountry(string countryName)
        {
            return _countryRepository.GetAll().FirstOrDefault(c => c.name == countryName);
        }
    }
}
