using Npgsql;
using System.Text.Json;
var cs=Environment.GetEnvironmentVariable("YODYSTORE_CONNECTION_STRING")??Environment.GetEnvironmentVariable("YODYSTORE_CONNECTION_STRING",EnvironmentVariableTarget.User);
await using var db=new NpgsqlConnection(cs); await db.OpenAsync();
var sql=await File.ReadAllTextAsync(args[0]);
await using var tx=await db.BeginTransactionAsync();
await using var cmd=new NpgsqlCommand(sql,db,tx);
await using(var reader=await cmd.ExecuteReaderAsync()){
 do{var rows=new List<Dictionary<string,object>>();while(await reader.ReadAsync()){var row=new Dictionary<string,object>();for(int i=0;i<reader.FieldCount;i++)row[reader.GetName(i)]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(row);}Console.WriteLine(JsonSerializer.Serialize(rows));}while(await reader.NextResultAsync());
}
await tx.CommitAsync();
