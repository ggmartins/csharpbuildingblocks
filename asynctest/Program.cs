namespace asynctest;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

class Result
{
    public string msg = "";
    public bool is_ok = false;
}

class Program
{
    private bool _Executing = false;
    public bool Executing { get => _Executing; set => _Executing = value; }

    public async Task<Result> Run1(string Msg, CancellationToken ct)
    {
      Console.WriteLine($"INFO:{nameof(Run1)} Executing...");
      await Task.Delay(10000, ct);
      Result result = new Result { msg =  Msg, is_ok = true };
      Console.WriteLine($"INFO:{nameof(Run1)} Done.");
      return result;
    }

    public async Task<Result> Run2(string Msg, CancellationToken ct)
    {
      Console.WriteLine($"INFO:{nameof(Run2)} Executing...");
      await Task.Delay(10000, ct);
      Result result = new Result { msg =  Msg, is_ok = true };
      Console.WriteLine($"INFO:{nameof(Run2)} Done.");
      return result;
    }

    public async Task<Result> Run3(string Msg, CancellationToken ct)
    {
      Console.WriteLine($"INFO:{nameof(Run3)} Executing...");
      await Task.Delay(100, ct);
      Result result = new Result { msg =  Msg, is_ok = true };
      Console.WriteLine($"INFO:{nameof(Run3)} Done.");
      return result;
    }

    public async Task<Result> Run4(string Msg, CancellationToken ct)
    {
      Console.WriteLine($"INFO:{nameof(Run4)} Executing...");
      await Task.Delay(100, ct);
      Console.WriteLine($"Executing {nameof(Run4)}");
      throw new Exception($"{nameof(Run4)} crashed");
      //Console.WriteLine($"INFO:{nameof(Run4)} Done.");
    }

    public async Task<Result> Run5(string Msg, CancellationToken ct)
    {
        Console.WriteLine($"INFO:{nameof(Run5)} Executing...");
        using (HttpClient client = new HttpClient())
        {
            HttpResponseMessage response = await client.GetAsync("https://www.google.com", ct);
            Console.WriteLine($"RESPONSE: {response.StatusCode}");
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync(ct);
            Console.WriteLine($"INFO:{nameof(Run5)} Done.");
            return new Result 
            { 
                msg = $"{Msg}: Request succeeded with status {response.StatusCode}", 
                is_ok = true 
            };
        }
    }


    public void Execute(CancellationTokenSource cts) 
    {
        /***
         *  This "defensive" coding experiment tries to
         *  catch potential problems running multiple async calls
         *  to functions that might *crash* or *timeout*. The code
         *  attempts list the exact tasks that timed out or crashed
         *  TODO:
         *    - use one CancellationTokenSource per function
         *    - Mutual exclusion of the entire Execute function
         *    - dead pool
         *    - python version
         */
        _Executing = true;
        var tests = new []
        {
             Run1("test1", cts.Token), //should timeout
             Run2("test2", cts.Token), //should timeout
             Run3("test3", cts.Token), //should execute
             Run4("test4", cts.Token), //should crash
             Run5("test5", cts.Token), //https get to google.com
        };

        var tests_named = Array.Empty<(string Name, Task<Result> Func)>();
        for (var i = 0; i < tests.Length; i++)
        {
           Console.WriteLine($"Processing: {tests[i]}");
           tests_named = tests_named.Append((Name:$"{tests[i]}", Func:tests[i])).ToArray();
        }
        Console.WriteLine($"Length: {tests_named.Length}");
        var incomplete = new List<(string Name, string Reason, string Error)>();
        var tests_exec = tests_named.Select(async x => 
            {
                // Updating delay here to > 10000 should make tasks Run1 and Run2 to run OK
                var completedTask = await Task.WhenAny(x.Func, Task.Delay(2000, cts.Token));
                if (completedTask != x.Func)
                {
                    lock (incomplete)
                    {
                        incomplete.Add((Name: x.Name, Reason: "Timeout", Error: "Timeout"));
                        cts.Cancel();
                    }
                }
                else
                {
                    try
                    {
                        await completedTask;
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"ERROR: Incomplete task: [{x.Name}] ({ex})");
                        incomplete.Add((Name: x.Name, Reason: "Exception", Error: $"{ex}"));
                        throw ex;
                    }
                    Console.WriteLine($"[Completed: {completedTask}]");
                }
            }).ToArray();
        try
        {
            Task.WaitAll(tests_exec);
        }
        catch(Exception ex)
        {
            Console.WriteLine($"ERROR: {ex}");
        }
        finally
        {
            _Executing = false;
        }
        Console.WriteLine("WARN: INCOMPLETE TASKS:");
        incomplete.ForEach(x => {
            Console.WriteLine($"  * [{x.Reason}: {x.Name} ({x.Error})]");
        });
    }

    static void Main(string[] args)
    {
        Program p = new Program();
        using (var cts = new CancellationTokenSource())
        { 
            p.Execute(cts);
        }
    }
}
