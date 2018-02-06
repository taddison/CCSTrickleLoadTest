using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace app
{
    class Program
    {
        private static SqlCommand[] _commands;

        static void Main(string[] args)
        {
            var connectionString = "server=localhost;initial catalog=CCSTrickleLoad;integrated security=sspi";
            var outFileName = $@"c:\temp\TVPResults_{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}.csv";
            var results = new StringBuilder();
            results.AppendLine("Time,TargetRows,ThreadCount,BatchSize,ElapsedMilliseconds");

            var targetRows = 1000000;

            //var threads = new int[] { 1, 2, 4, 8, 16};
            var threads = Enumerable.Range(1, 10).ToArray();
            var batchSizes = new int[] { /*1, 10, 100, */ 1000, 10000, 100000, 1000000 };
            var repeats = 5;
            
            var combos = from thread in threads
                         from batchSize in batchSizes
                         from repeat in Enumerable.Range(0, repeats)
                         orderby thread descending, batchSize descending
                         select new { ThreadCount = thread, BatchSize = batchSize };

            foreach(var combo in combos)
            {
                if(_commands != null)
                {
                    _commands.ToList().ForEach(c =>
                    {
                        c.Dispose();
                        c.Connection.Close();
                    }
                    );
                }
                _commands = new SqlCommand[combo.ThreadCount];
                for(var i = 0; i < _commands.Length; i++)
                {
                    var conn = new SqlConnection(connectionString);
                    conn.Open();
                    var cmd = new SqlCommand("dbo.InsertBatch", conn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    _commands[i] = cmd;
                }

                var batches = targetRows / combo.BatchSize / combo.ThreadCount;

                if(batches == 0)
                {
                    Console.WriteLine("Insufficient target rows for test, skipping");
                    continue;
                }
                
                PrepareForTest(connectionString);

                var sw = Stopwatch.StartNew();
                Parallel.For(0, combo.ThreadCount,loopId => {
                    RunInnerLoop(loopId, combo.BatchSize, batches);
                });
                sw.Stop();
                Console.WriteLine($"{targetRows} total rows, {combo.ThreadCount} threads, {combo.BatchSize} per batch for {batches} batches per thread. {sw.ElapsedMilliseconds}ms total run time.");
                results.AppendLine($"{DateTime.UtcNow},{targetRows},{combo.ThreadCount},{combo.BatchSize},{sw.ElapsedMilliseconds}");
            }

            System.IO.File.WriteAllText(outFileName, results.ToString());
        }

        private static void PrepareForTest(string connectionString)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("truncate table dbo.CCSTrickle",conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void RunInnerLoop(int loopId, int batchSize, int batches)
        {
            var sw = Stopwatch.StartNew();
            var cmd = _commands[loopId];
            for(var i = 0; i < batches; i++)
            {
                cmd.Parameters.Clear();
                var tvp = BuildTVP(batchSize);
                cmd.Parameters.Add(tvp);
                cmd.ExecuteNonQuery();
            }
            sw.Stop();
            //Console.WriteLine($"Loop {loopId} wrote {batches * batchSize} rows in {sw.ElapsedMilliseconds}ms");
        }

        private static SqlParameter BuildTVP(int rows)
        {
            var dt = new DataTable();
            dt.Columns.Add("EventId", typeof(Guid));
            dt.Columns.Add("EventDateTime", typeof(DateTime));
            dt.Columns.Add("EventName", typeof(string));
            dt.Columns.Add("EventPayload", typeof(string));

            for(var i = 0; i < rows; i++)
            {
                dt.Rows.Add(new object[] {
                    Guid.NewGuid()
                    ,DateTime.UtcNow
                    ,"IncomingTVP"
                    ,"{WithAdded:'Payload'}"
                });
            }

            var param = new SqlParameter("@data", SqlDbType.Structured)
            {
                TypeName = "dbo.TrickleType"
                ,Value = dt
            };
            return param;
        }
    }
}
