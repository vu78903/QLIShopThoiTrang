using System.Text.Json;
using FashionStore.Backend.Data;
using FashionStore.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace FashionStore.Backend.Tests;

public sealed class CustomerMappingTests
{
    private static FashionStoreDbContext Context() => new(new DbContextOptionsBuilder<FashionStoreDbContext>()
        .UseSqlServer("Server=127.0.0.1,1;Database=FashionStoreDB_Dev;Integrated Security=True;Connect Timeout=1").Options);
    [Fact] public void AllMappedColumnsAndKeysMatchUserExport()
    {
        using var db=Context();
        using var schema=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"SchemaReference.json")));
        var columns=schema.RootElement.GetProperty("columns").EnumerateArray().ToArray();
        var keys=schema.RootElement.GetProperty("keys").EnumerateArray().ToArray();
        foreach(var entity in db.Model.GetEntityTypes()){
            var table=entity.GetTableName()!;var store=StoreObjectIdentifier.Table(table,entity.GetSchema());
            foreach(var property in entity.GetProperties()){
                var name=property.GetColumnName(store);
                var actual=Assert.Single(columns,c=>c.GetProperty("object").GetString()==table && c.GetProperty("name").GetString()==name);
                Assert.Equal(actual.GetProperty("nullable").GetBoolean(),property.IsNullable);
                if(property.GetMaxLength() is int max){
                    var factor=property.IsUnicode()==false?1:property.ClrType==typeof(string)?2:1;
                    Assert.Equal(actual.GetProperty("maxLengthBytes").GetInt32(),max*factor);
                }
                var sqlType=property.GetColumnType()!.Split('(')[0].ToLowerInvariant();
                var actualType=actual.GetProperty("typeName").GetString();
                Assert.True(sqlType==actualType || (sqlType=="rowversion" && actualType=="timestamp"),$"{table}.{name}: {sqlType} vs {actualType}");
                if(property.GetPrecision() is int precision)Assert.Equal(actual.GetProperty("precision").GetInt32(),precision);
                if(property.GetScale() is int scale)Assert.Equal(actual.GetProperty("scale").GetInt32(),scale);
            }
            var actualKey=keys.Where(k=>k.GetProperty("table").GetString()==table && k.GetProperty("keyType").GetString()=="PK")
                .OrderBy(k=>k.GetProperty("ordinal").GetInt32()).Select(k=>k.GetProperty("column").GetString());
            Assert.Equal(actualKey,entity.FindPrimaryKey()!.Properties.Select(p=>p.GetColumnName(store)));
        }
    }
    [Fact] public void ActualCartReadQueryTranslatesToSqlWithOwnerPredicateAndCurrentVariantPrice()
    {
        using var db=Context();var service=new CartService(db,TimeProvider.System);
        var sql=service.ReadQuery("CUSTOMER-OWNER").ToQueryString();
        Assert.Contains("ChiTietGioHang",sql);Assert.Contains("GioHang",sql);
        Assert.Contains("BienTheSanPham",sql);Assert.Contains("DonGia",sql);
        Assert.Contains("MaKH",sql);Assert.Contains("CUSTOMER-OWNER",sql);
        Assert.Contains("ORDER BY",sql);
    }
}
