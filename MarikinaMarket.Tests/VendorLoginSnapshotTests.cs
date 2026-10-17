using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MarikinaMarket.Tests;

public class VendorLoginSnapshotTests
{
    [Fact]
    public async Task Login_account_snapshot_includes_owned_business_and_scannable_qr_value()
    {
        await using var host = await VendorSelfProfileHost.CreateAsync();
        using var response = await host.Send(HttpMethod.Get, path: "/api/user/me?user_id=88&vendor_id=7");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, snapshot.GetProperty("userId").GetInt32());
        var vendor = snapshot.GetProperty("vendor");
        Assert.Equal(70, vendor.GetProperty("vendor_id").GetInt32());
        Assert.Equal("MM-70", vendor.GetProperty("business_id").GetString());
        Assert.Equal("Reyes Fresh Fish", vendor.GetProperty("business_name").GetString());
        Assert.Equal("test-only", vendor.GetProperty("qr_code_value").GetString());
    }

    [Fact]
    public async Task Online_edit_returns_vendor_snapshot_for_cache_replacement()
    {
        await using var host = await VendorSelfProfileHost.CreateAsync();
        using var response = await host.Send(HttpMethod.Put, body: host.Edit());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Juana", result.GetProperty("user").GetProperty("firstName").GetString());
        Assert.Equal(70, result.GetProperty("user").GetProperty("vendor").GetProperty("vendor_id").GetInt32());
        Assert.Equal("test-only", result.GetProperty("vendor").GetProperty("qr_code_value").GetString());
        Assert.Equal(1, host.Writes);
    }
}
