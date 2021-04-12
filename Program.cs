using System;
using OrderCloud.SDK;
using OrderCloud.Catalyst;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;

namespace ThrottlingRepro
{
    /// <summary>
    /// To repro locally
    /// 1. Create a new organization
    /// 2. Create a new API Client with a ClientSecret and Seller Access
    /// 3. Create an admin user with Active set to true
    /// 4. Create a security profile with FullAccess
    /// 5. Assign security profile to admin user
    /// 6. Set default context user of API client to admin user in step 3
    /// 7. Set environment variables ClientID and ClientSecret
    /// 8. Run this and observe there are no errors related to 429 at concurrency of 30 parallel requests
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            CreateProductsAsync().GetAwaiter().GetResult();
        }

        static async Task CreateProductsAsync()
        {
            try
            {
                var clientID = Environment.GetEnvironmentVariable("ClientID");
                var clientSecret = Environment.GetEnvironmentVariable("ClientSecret");
                var sdk = new OrderCloudClient(new OrderCloudClientConfig
                {
                    ClientId = clientID,
                    ClientSecret = clientSecret,
                    AuthUrl = "https://sandboxapi.ordercloud.io",
                    ApiUrl = "https://sandboxapi.ordercloud.io",
                    Roles = new ApiRole[] {
                    ApiRole.FullAccess,
                    ApiRole.ProductAdmin
                }
                });
                await sdk.AuthenticateAsync();
                var products = GetProductsToSave();
                var tasks = products.Select(p => sdk.Products.SaveAsync(p.ID, p));

                var concurrent = 30; // bug should happen at 15 so this should definitely trigger it
                await Throttler.RunAsync(products, 0, concurrent, async (product) => {
                    await sdk.Products.SaveAsync(product.ID, product);
                    Console.WriteLine($"Saved product {product.ID}");
                });
            } catch(OrderCloudException ex)
            {
                if(ex.HttpStatus == HttpStatusCode.TooManyRequests)
                {
                    Console.WriteLine("Throttled requests");
                }
            }
        }

        private static IEnumerable<Product> GetProductsToSave()
        {
            var count = 240;
            return Enumerable.Range(0, count).Select(index => new Product
            {
                ID = $"Product-{index + 1}",
                Name = $"Product-{index + 1}",
                QuantityMultiplier = 1
            });
        }
    }
}
