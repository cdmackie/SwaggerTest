using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Configuration;

using Coins;
using Coins.Models;
using Coins.Services;
using Coins.API;

namespace Coins.Services.Tests
{
	public class MockDataRepository : IDataRepository
  {
		class Table
		{
			public Type Type { get; set; }
			public int NextId { get; set; }
			public Dictionary<int, object> Rows { get; set; }

			public Table(Type type)
			{
				NextId = 1;
				Type = type;
				Rows = new Dictionary<int, object>();
			}
		}

		private Dictionary<Type, Table> _data = new Dictionary<Type, Table>();

		private Dictionary<Type, Table> _lastdata = new Dictionary<Type, Table>();

		public MockDataRepository()
		{
		}

    public void InitData(string dir, IAppSettings appSettings)
    {

    }

    public void Commit()
		{
			_lastdata = new Dictionary<Type, Table>();
			foreach (var entry in _data)
			{
				var table = entry.Value;
				var tablecopy = new Table(entry.Key);
				tablecopy.NextId = table.NextId;
				foreach (var obj in table.Rows)
				{
					tablecopy.Rows.Add(obj.Key, obj.Value.CreateCopy());
				}

				_lastdata.Add(entry.Key, tablecopy);
			}
		}

		public void Rollback()
		{
			_data = new Dictionary<Type, Table>();
			foreach (var entry in _lastdata)
			{
				var table = entry.Value;
				var tablecopy = new Table(entry.Key);
				tablecopy.NextId = table.NextId;
				foreach (var obj in table.Rows)
				{
					tablecopy.Rows.Add(obj.Key, obj.Value.CreateCopy());
				}

				_data.Add(entry.Key, tablecopy);
			}
		}

		private Table GetTable(Type type)
		{
			Table table;
			if (_data.TryGetValue(type, out table) == false)
			{
				table = new Table(type);
				_data.Add(type, table);
			}
			return table;
		}

		private List<T> Where<T>(object keys)
		{
			var table = GetTable(typeof(T));
			List<T> objs = table.Rows.Values.Cast<T>().ToList();
			var pis = typeof(T).GetTypeInfo().GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
			if (keys != null)
			{
				List<T> remove = new List<T>();
				foreach (var obj in objs)
				{
					foreach (var key in keys.GetType().GetTypeInfo().GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public))
					{
						object v = key.GetValue(keys);
						if (v != null)
						{
							var pi = pis.Where(p => p.Name == key.Name).FirstOrDefault();
							if (pi == null)
							{
								throw new BadArgument("No such property " + key.Name, key.Name);
							}

							if (v.Equals(pi.GetValue(obj)) == false)
							{
								remove.Add(obj);
								break;
							}
						}
					}
				}

				foreach (var obj in remove)
				{
					objs.Remove(obj);
				}
			}

			return objs;
		}

		public Task<T> Get<T>(int id, bool skipCache = false) where T : IHasId<int>
		{
			var table = GetTable(typeof(T));

			object obj;
			if (table.Rows.TryGetValue(id, out obj) == true)
			{
				return Task.FromResult(((T)obj).CreateCopy<T>());
			}

			return Task.FromResult(default(T));
		}

    public async Task<List<T>> GetAsync<T>(object keys, DataPaging paging = null) where T : IHasId<int>
    {
      var list = Get<T>(keys, paging);
      return await Task.FromResult<List<T>>(list);
    }
    public List<T> Get<T>(object keys, DataPaging paging = null) where T : IHasId<int>
    {
			if (paging == null)
			{
				paging = new DataPaging { WithTotal = false };
			}

			List<T> objs = Where<T>(keys);

			var sorted = objs.OrderBy(x => x.Id);
			var sorts = (paging.Sort ?? string.Empty).Split(',').Select(s => s.Trim()).Where(s => s.Length != 0).ToArray();
			for (var i=0; i<sorts.Length; i++)
			{
				string sort = sorts[i];

				bool desc = false;
				if (sort.StartsWith("-") == true)
				{
					desc = true;
					sort = sort.Substring(1);
				}

				var pi = typeof(T).GetTypeInfo().GetProperty(sort, BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public);
				if (i == 0)
				{
					if (desc == true)
					{
						sorted = sorted.OrderByDescending(x => pi.GetValue(x));
					}
					else
					{
						sorted = sorted.OrderBy(x => pi.GetValue(x));
					}
				}
				else
				{
					if (desc == true)
					{
						sorted = sorted.ThenByDescending(x => pi.GetValue(x));
					}
					else
					{
						sorted = sorted.ThenBy(x => pi.GetValue(x));
					}
				}
			}
			objs = sorted.ToList();

			if (paging.WithTotal == true)
			{
				paging.Total = objs.Count;
			}

			if (paging.Start != 0)
			{
				objs = objs.Skip(paging.Start).ToList();
			}
			if (paging.Count != 0)
			{
				objs = objs.Take(paging.Count).ToList();
			}

			// we have to clone each of the object in case they are changed by the caller
			List<T> cloned = new List<T>();
			foreach (var obj in objs)
			{
				cloned.Add(((T)obj).CreateCopy<T>());
			}
			objs = cloned;

			return objs;
		}

		public int Total<T>(object keys) where T : IHasId<int>
    {
			List<T> objs = Where<T>(keys);

			return objs.Count;
		}
    public async Task<int> TotalAsync<T>(object keys) where T : IHasId<int>
    {
      int total = Total<T>(keys);

      return await Task.FromResult(total);
    }

    public Task<T> Create<T>(T obj) where T : IHasId<int>
    {
			var table = GetTable(typeof(T));

			if (obj.Id == 0)
			{
				obj.Id = table.NextId++;
			}
			table.Rows.Add(obj.Id, obj);

			return Task.FromResult(obj);
		}

		public T Update<T>(T obj) where T : IHasId<int>
    {
			var table = GetTable(typeof(T));

			object item;
			if (table.Rows.TryGetValue(((IHasId<int>)obj).Id, out item) == false)
			{
				throw new Exception("Update object not found");
			}

			((T)item).PopulateWithNonDefaultValues<T, T>(obj);

			return ((T)item).CreateCopy<T>();
		}
    public async Task<T> UpdateAsync<T>(T obj) where T : IHasId<int>
    {
      obj = Update<T>(obj);

      return await Task.FromResult(obj);
    }

    public void DeleteAll<T>(object keys) where T : IHasId<int>
    {
			var table = GetTable(typeof(T));

			var list = Get<T>(keys);
			foreach (var obj in list)
			{
				table.Rows.Remove(obj.Id);
			}
		}
    public async Task DeleteAllAsync<T>(object keys) where T : IHasId<int>
    {
      DeleteAll<T>(keys);
      await Task.CompletedTask;
    }

    public void Delete<T>(T obj) where T : IHasId<int>
    {
			var table = GetTable(typeof(T));

			obj = Get<T>(obj.Id).Result;
			if (obj != null)
			{
				table.Rows.Remove(obj.Id);
			}
		}
    public async Task DeleteAsync<T>(T obj) where T : IHasId<int>
    {
      Delete<T>(obj);
      await Task.CompletedTask;
    }

    public R GetScalar<T, R>(string sql, object keys) where T : IHasId<int>
    {

    }
    public async Task<R> GetScalarAsync<T, R>(string sql, object keys) where T : IHasId<int>
    {

    }

    public Dictionary<string, Country> GetCountries()
		{
			var countries = new Dictionary<string, Country>();
			foreach (var country in Country.DEFAULTS)
			{
				countries.Add(country.Code, country);
			}
			return countries;
		}

		public Country GetCountry(string code)
		{
			return Country.DEFAULTS.Where(c => c.Code == code).FirstOrDefault();
		}

		public Dictionary<int, CountryTaxyear> GetCountryTaxyears(string country)
		{
			var list = new Dictionary<int, CountryTaxyear>();
			foreach (var cty in CountryTaxyear.DEFAULTS)
			{
				if (cty.Country == country)
				{
					list.Add(cty.Taxyear, cty);
				}
			}
			return list;
		}

		public CountryTaxyear GetCountryTaxyears(string country, int taxyear)
		{
			foreach (var cty in CountryTaxyear.DEFAULTS)
			{
				if (cty.Country == country && cty.Taxyear == taxyear)
				{
					return cty;
				}
			}
			return null;
		}

		public Dictionary<string, Currency> GetCurrencies()
		{
			var currencies = new Dictionary<string, Currency>();
			foreach (var currency in Currency.DEFAULTS)
			{
				currencies.Add(currency.Code, currency);
			}
			return currencies;
		}

		public Task<List<Daily>> GetDaily(long from, long to, string pair, string exchange = null, DataPaging paging = null)
		{
			throw new NotImplementedException();
		}

		public Tuple<long, long> GetPairFirstAndLast(string pair)
		{
			throw new NotImplementedException();
		}

		public Task<List<string>> GetPairs(long from = 0, long to = 0, string exchange = null)
		{
			throw new NotImplementedException();
		}

		public async Task<List<Trade>> GetTrades(int userId, int taxyear, DataPaging paging = null)
		{
			return await GetAsync<Trade>(new { UserId = userId, Taxyear = taxyear }, paging);
		}

		public async Task<List<Trade>> GetTrades(int userId, int taxyear, Actions? action = null, string exchange = null, string exchangeId = null, DataPaging paging = null)
		{
			return await GetAsync<Trade>(new { UserId = userId, Taxyear = taxyear, Action = action, Exchange = exchange, ExchangeId = exchangeId }, paging);
		}

		public UserAuth GetUser(int id)
		{
			var table = GetTable(typeof(UserAuth));
			object user = null;
			table.Rows.TryGetValue(id, out user);
			return (UserAuth)user;
		}

		public UserAuth GetUser(string usernameOrEmail)
		{
			var table = GetTable(typeof(UserAuth));
			var list = table.Rows.Values.ToList();
			return list.Cast<UserAuth>().Where(u => u.Email == usernameOrEmail || u.UserName == usernameOrEmail).FirstOrDefault();
		}

		public UserAuth CreateUser(UserAuth user)
		{
			var table = GetTable(typeof(UserAuth));
			user.Id = table.NextId++;
			table.Rows.Add(user.Id, user);
			return user;
		}

		public void DeleteUser(UserAuth user)
		{
			throw new NotImplementedException();
		}

		public List<UserAuthDetails> GetUserAuthDetails(int userId)
		{
			throw new NotImplementedException();
		}

		public UserAuthDetails GetUserAuthDetails(string provider, string userId)
		{
			throw new NotImplementedException();
		}

		public bool IsCommonPassword(string password)
		{
			return false;
		}

		public UserAuthDetails CreateUserAuthDetails(UserAuthDetails user)
		{
			throw new NotImplementedException();
		}

		public void DeleteUserAuthDetails(UserAuthDetails user)
		{
			throw new NotImplementedException();
		}

		public UserAuth UpdateUser(UserAuth user)
		{
			throw new NotImplementedException();
		}

		public UserAuthDetails UpdateUserAuthDetails(UserAuthDetails user)
		{
			throw new NotImplementedException();
		}
	}
}
