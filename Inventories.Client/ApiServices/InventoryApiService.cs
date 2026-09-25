using Duende.IdentityModel.Client;
using Inventories.Client.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Newtonsoft.Json;

namespace Inventories.Client.ApiServices
{
    public class InventoryApiService : IInventoryApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly DiscoveryPolicy _discoveryPolicy;

        public InventoryApiService(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, DiscoveryPolicy discoveryPolicy)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _discoveryPolicy = discoveryPolicy ?? throw new ArgumentNullException(nameof(discoveryPolicy));
        }

        public async Task<IEnumerable<Inventory>> GetInventories()
        {
            var httpClient = _httpClientFactory.CreateClient("InventoryAPIClient");

            var request = new HttpRequestMessage(HttpMethod.Get, "/Inventories");

            var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var inventoryList = JsonConvert.DeserializeObject<List<Inventory>>(content);
            
            return inventoryList;

        }

        public async Task<Inventory> CreateInventory(Inventory inventory)
        {
            var httpClient = _httpClientFactory.CreateClient("InventoryAPIClient");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Inventories")
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(inventory),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };

            var response = await httpClient.SendAsync(request).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Inventory>(content);
        }

        public async Task DeleteInventory(int id)
        {
            var httpClient = _httpClientFactory.CreateClient("InventoryAPIClient");

            var request = new HttpRequestMessage(HttpMethod.Delete, $"/Inventories/{id}");

            var response = await httpClient.SendAsync(request).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
        }

        public async Task<Inventory> GetInventory(string id)
        {
            var httpClient = _httpClientFactory.CreateClient("InventoryAPIClient");

            var request = new HttpRequestMessage(HttpMethod.Get, $"/Inventories/{id}");

            var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Inventory>(content);
        }

        public async Task<Inventory> UpdateInventory(Inventory inventory)
        {
            var httpClient = _httpClientFactory.CreateClient("InventoryAPIClient");

            var request = new HttpRequestMessage(HttpMethod.Put, $"/Inventories/{inventory.Id}")
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(inventory),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };

            var response = await httpClient.SendAsync(request).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return inventory;
        }

        public async Task<UserInfoViewModel> GetUserInfo()
        {
            var idpClient = _httpClientFactory.CreateClient("IDPClient");

            var metaDataResponse = await idpClient.GetDiscoveryDocumentAsync(
                new DiscoveryDocumentRequest { Policy = _discoveryPolicy });

            if (metaDataResponse.IsError) 
            {
                throw new HttpRequestException("Something went wrong while requesting the access token");
            }

            var accessToken = await _httpContextAccessor
                .HttpContext.GetTokenAsync(OpenIdConnectParameterNames.AccessToken);

            var userInfoResponse = await idpClient.GetUserInfoAsync(
               new UserInfoRequest
               {
                   Address = metaDataResponse.UserInfoEndpoint,
                   Token = accessToken
               });
            
            if (userInfoResponse.IsError)
            {
                throw new HttpRequestException("Something went wrong while getting user info");
            }

            var userInfoDictionary = new Dictionary<string, string>();

            foreach (var claim in userInfoResponse.Claims)
            {
                userInfoDictionary.Add(claim.Type, claim.Value);
            }

            return new UserInfoViewModel(userInfoDictionary);
        }
    }
}
