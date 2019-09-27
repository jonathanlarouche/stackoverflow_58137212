using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace _58137212
{
    class Program
    {
        static void Main(string[] args)
        {
            string apiToken = "goodtoken";
            GetCustomFields(apiToken, Enumerable
                .Range(1,25)
                .Select(i => Guid.NewGuid())
                .ToArray());

            Console.WriteLine("Pool size at finish: {0}", ApiHelper.PoolSize);
            Console.ReadKey();
        }

        static public void GetCustomFields(string apiToken, IEnumerable<Guid> guids)
        {
            //format body
            string jsonBody = "{}";

            var responses = new List<Task<string>>();
            foreach (Guid g in guids)
            {
                responses.Add(GetData(apiToken, g, jsonBody));

            }
            Task.WaitAll(responses.ToArray());
        }

        async static Task<string> GetData(string apiToken, Guid guid, string jsonBody)
        {

            string url = "https://api.elliemae.com/encompass/v1/loans/" + guid + "/fieldReader";
            Console.WriteLine("{0} has started .....", guid);
            string output = null;
            await ApiHelper.Use(apiToken, (client) => 
            {
                var json = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                return client.PostAsync(url, json).ContinueWith(postTaskResult =>
                {

                    return postTaskResult.Result.Content.ReadAsStringAsync().ContinueWith(s => {

                        output = s.Result;
                        return s;
                    });
                });
            });
            Console.WriteLine("{0} has finished .....", guid);
            return output;
        }
    }

    public static class ApiHelper
    {
        public static int PoolSize { get => apiClientPool.Size; }

        private static ArrayPool<HttpClient> apiClientPool = new ArrayPool<HttpClient>(() => {
            var apiClient = new HttpClient();
            apiClient.DefaultRequestHeaders.Add("ContentType", "application/json");
            return apiClient;
        });

        public static Task Use(string apiToken, Func<HttpClient, Task> action)
        {
            return apiClientPool.Use(client => {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
                return action(client);
            });
        }
    }

    public class ArrayPool<T>
    {
        public int Size { get => pool.Count(); }
        public int maxSize = 3;
        public int circulingObjectCount = 0;
        private Queue<T> pool = new Queue<T>();
        private Func<T> constructorFunc;

        public ArrayPool(Func<T> constructorFunc) {
            this.constructorFunc = constructorFunc;
        }

        public Task Use(Func<T, Task> action)
        {
            T item = GetNextItem(); //DeQueue the item
            var t = action(item);
            t.ContinueWith(task => pool.Enqueue(item)); //Requeue the item
            return t;
        }

        private T GetNextItem()
        {
            //Create new object if pool is empty and not reached maxSize
            if (pool.Count == 0 && circulingObjectCount < maxSize)
            {
                T item = constructorFunc();
                circulingObjectCount++;
                Console.WriteLine("Pool empty, adding new item");
                return item;
            }
            //Wait for Queue to have at least 1 item
            WaitForReturns();

            return pool.Dequeue();
        }

        private void WaitForReturns()
        {
            long timeouts = 60000;
            while (pool.Count == 0 && timeouts > 0) { timeouts--; System.Threading.Thread.Sleep(1); }
            if(timeouts == 0)
            {
                throw new Exception("Wait timed-out");
            }
        }
    }
}
