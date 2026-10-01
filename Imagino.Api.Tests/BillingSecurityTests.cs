#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Imagino.Api.Controllers;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Security;
using Imagino.Api.Services.Billing;
using Imagino.Api.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using Newtonsoft.Json;
using Stripe;
using Xunit;
using User = Imagino.Api.Models.User;

namespace Imagino.Api.Tests;
public class BillingSecurityTests
{
    private const string Preview = "https://imagino-front-git-fix-revival-opera-a2526a-danitest45s-projects.vercel.app";
    private const string SigningSecret = "whsec_local_synthetic_test_only";
    private static Dictionary<string,string?> Config() => new() {
        ["ImageGeneratorSettings:MongoConnection"]="mongodb://localhost:27017",
        ["ImageGeneratorSettings:MongoDatabase"]="imagino_staging",
        ["Jwt:Secret"]="local-test-key-longer-than-thirty-two-bytes",
        ["Jwt:Issuer"]="tests", ["Jwt:Audience"]="tests", ["Frontend:BaseUrl"]=Preview,
        ["RefreshTokenCookie:HttpOnly"]="true",["RefreshTokenCookie:Secure"]="true",["RefreshTokenCookie:SameSite"]="None",
        ["RefreshTokenCookie:ExpiresDays"]="7",
        ["Stripe:ApiKey"]="sk_test_local_fixture", ["Stripe:WebhookSecret"]=SigningSecret,
        ["Stripe:PricePro"]="price_pro_fixture", ["Stripe:PriceUltra"]="price_ultra_fixture",
        ["Stripe:SuccessUrl"]=Preview+"/checkout/success", ["Stripe:CancelUrl"]=Preview+"/checkout/cancel",
        ["Stripe:PortalReturnUrl"]=Preview+"/profile"
    };
    [Fact] public void EveryPartialStripeConfigurationFailsWithoutValues()
    {
        for(var mask=1; mask<127; mask++) {
            var cfg=Config();
            for(var i=0;i<7;i++) if((mask & (1<<i))==0) cfg.Remove(StartupConfiguration.StripeKeys[i]);
            var ex=Assert.Throws<InvalidOperationException>(()=>StartupConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(cfg).Build(),false));
            Assert.DoesNotContain(SigningSecret,ex.Message);
            Assert.DoesNotContain("sk_test_local_fixture",ex.Message);
        }
        var disabled=Config(); foreach(var key in StartupConfiguration.StripeKeys) disabled.Remove(key);
        StartupConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(disabled).Build(),false);
        StartupConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(Config()).Build(),false);
    }
    [Theory]
    [InlineData("Stripe:ApiKey","sk_live_synthetic")]
    [InlineData("Stripe:ApiKey","rk_live_synthetic")]
    [InlineData("Stripe:SuccessUrl","https://imagino-front.vercel.app/checkout/success")]
    [InlineData("Stripe:CancelUrl","http://example.test/cancel")]
    [InlineData("Stripe:PortalReturnUrl","https://attacker.test/profile")]
    public void UnsafeConfigurationFails(string key,string value)
    {
        var cfg=Config();cfg[key]=value;
        var ex=Assert.Throws<InvalidOperationException>(()=>StartupConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(cfg).Build(),false));
        Assert.DoesNotContain(value,ex.Message);
    }
    [Fact] public async Task IdentityUsesSubThenFallbackAndFailsClosed()
    {
        var identity=new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","preferred"),new Claim(ClaimTypes.NameIdentifier,"fallback")],"test"));
        Assert.Equal("preferred",BillingController.Identity(identity));
        Assert.Equal("fallback",BillingController.Identity(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"fallback")],"test"))));
        var service=new Mock<IBillingService>();
        var controller=new BillingController(service.Object,new Mock<IUserRepository>().Object,NullLogger<BillingController>.Instance) {
            ControllerContext=new ControllerContext {HttpContext=new DefaultHttpContext()}
        };
        Assert.IsType<UnauthorizedResult>(await controller.CreateCheckoutSession(new("PRO")));
        Assert.IsType<UnauthorizedResult>(await controller.CreatePortalSession());
        Assert.IsType<UnauthorizedResult>(await controller.GetSubscription());
        service.VerifyNoOtherCalls();
    }
    [Theory] [InlineData("checkout")] [InlineData("portal")] [InlineData("me")]
    public async Task AnonymousBillingIs401(string action)
    {
        using var factory=new SecurityApiFactory();
        using var client=factory.CreateClient();
        var r=action=="me"?await client.GetAsync("/api/billing/me"):await client.PostAsync("/api/billing/"+action,new StringContent("{\"plan\":\"PRO\"}",Encoding.UTF8,"application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized,r.StatusCode);
    }
    [Theory]
    [InlineData("PriceId")] [InlineData("Credits")] [InlineData("Amount")]
    [InlineData("currency")] [InlineData("quantity")] [InlineData("StripeCustomerId")] [InlineData("SubscriptionStatus")]
    public async Task CheckoutRejectsEconomicPayloadFields(string field)
    {
        using var factory=Factory();using var client=factory.ClientFor("user-a");
        var r=await client.PostAsync("/api/billing/checkout",new StringContent("{\"plan\":\"PRO\",\""+field+"\":\"untrusted\"}",Encoding.UTF8,"application/json"));
        Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);
        factory.Stripe.VerifyNoOtherCalls();
    }
    [Theory] [InlineData("GOLD")] [InlineData("")] [InlineData("pro")]
    public async Task InvalidPlanReturns400BeforeCustomerCreation(string plan)
    {
        using var factory=Factory();using var client=factory.ClientFor("user-a");
        var r=await client.PostAsync("/api/billing/checkout",new StringContent(JsonConvert.SerializeObject(new {plan}),Encoding.UTF8,"application/json"));
        Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);
        factory.Stripe.VerifyNoOtherCalls();
    }
    [Fact] public async Task MissingUserAndCustomerHaveAppropriateErrors()
    {
        using var factory=Factory();using var client=factory.ClientFor("user-a");
        var payload=new StringContent("{\"plan\":\"PRO\"}",Encoding.UTF8,"application/json");
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync("/api/billing/checkout",payload)).StatusCode);
        factory.UserRepository.Setup(x=>x.GetByIdAsync("user-a")).ReturnsAsync(new User {Id="user-a"});
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync("/api/billing/portal",null)).StatusCode);
        factory.Stripe.VerifyNoOtherCalls();
    }
    [Fact] public async Task CheckoutResolvesServerPriceAndRejectsExistingSubscription()
    {
        using var factory=Factory();
        var user=new User {Id="user-a",StripeCustomerId="cus_fixture"};
        factory.UserRepository.Setup(x=>x.GetByIdAsync("user-a")).ReturnsAsync(user);
        Stripe.Checkout.SessionCreateOptions? actual=null;
        RequestOptions? request=null;
        factory.Stripe.Setup(x=>x.CreateCheckoutAsync(It.IsAny<Stripe.Checkout.SessionCreateOptions>(),It.IsAny<RequestOptions>()))
            .Callback<Stripe.Checkout.SessionCreateOptions,RequestOptions>((o,r)=>{actual=o;request=r;})
            .ReturnsAsync(new Stripe.Checkout.Session {Url="https://checkout.stripe.com/synthetic"});
        using var client=factory.ClientFor("user-a");
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/api/billing/checkout",new StringContent("{\"plan\":\"PRO\"}",Encoding.UTF8,"application/json"))).StatusCode);
        Assert.Equal("price_pro_fixture",Assert.Single(actual!.LineItems).Price);
        Assert.Equal(1,actual.LineItems[0].Quantity);
        Assert.Null(actual.PaymentMethodTypes);
        Assert.Equal(Preview+"/checkout/success",actual.SuccessUrl);
        Assert.DoesNotContain("session_id",actual.SuccessUrl);
        Assert.False(string.IsNullOrEmpty(request!.IdempotencyKey));
        user.StripeSubscriptionId="sub_fixture";user.SubscriptionStatus="active";
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync("/api/billing/checkout",new StringContent("{\"plan\":\"ULTRA\"}",Encoding.UTF8,"application/json"))).StatusCode);
        factory.Stripe.Verify(x=>x.CreateCheckoutAsync(It.IsAny<Stripe.Checkout.SessionCreateOptions>(),It.IsAny<RequestOptions>()),Times.Once);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task UnsignedOrInvalidSignatureCannotClaimOrCredit(bool invalid)
    {
        using var factory=Factory();using var client=factory.CreateClient();
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/stripe/webhook") {Content=new StringContent("{}")};
        if(invalid)request.Headers.Add("Stripe-Signature","t=1,v1=invalid");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.SendAsync(request)).StatusCode);
        factory.StripeEvents.VerifyNoOtherCalls();factory.UserRepository.VerifyNoOtherCalls();
    }
    [Fact] public async Task PaidCheckoutAndBothInvoiceEventTypesGrantInitialAllowanceOnce()
    {
        using var fixture=new WebhookFixture();
        var invoice=Invoice("in_initial","price_pro_fixture","subscription_create");
        fixture.Factory.Stripe.Setup(s=>s.GetInvoiceAsync("in_initial")).ReturnsAsync(invoice);
        var session=new Stripe.Checkout.Session {Object="checkout.session",Id="cs_fixture",Mode="subscription",PaymentStatus="paid",
            CustomerId="cus_fixture",ClientReferenceId="user-a",SubscriptionId="sub_fixture",InvoiceId="in_initial"};
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_checkout","checkout.session.completed",session));
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_checkout","checkout.session.completed",session));
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_invoice_paid","invoice.paid",invoice));
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_invoice_succeeded","invoice.payment_succeeded",invoice));
        Assert.Equal(100,fixture.State.Credits);Assert.Single(fixture.CreditKeys);
        Assert.Equal(3,fixture.Journal.Count);
        Assert.Equal("PRO",fixture.State.Plan);
    }
    [Fact] public async Task RenewalAndDualInvoiceConcurrencyGrantOncePerInvoice()
    {
        using var fixture=new WebhookFixture();
        var invoice=Invoice("in_renewal","price_ultra_fixture","subscription_cycle");
        fixture.State.Credits=100;
        var result=await Task.WhenAll(fixture.Send("evt_paid","invoice.paid",invoice),fixture.Send("evt_succeeded","invoice.payment_succeeded",invoice));
        Assert.All(result,r=>Assert.Equal(HttpStatusCode.OK,r));
        Assert.Equal(400,fixture.State.Credits);Assert.Single(fixture.CreditKeys);
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_paid","invoice.paid",invoice));
        Assert.Equal(400,fixture.State.Credits);
    }
    [Fact] public async Task TwoConcurrentRequestsSameEventHaveOneEffectAndSafeReplay()
    {
        using var fixture=new WebhookFixture();
        var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Factory.Stripe.Setup(s=>s.GetSubscriptionAsync("sub_fixture")).Returns(async ()=>{
            entered.TrySetResult(true);await release.Task;return Subscription("price_pro_fixture");});
        var invoice=Invoice("in_concurrent","price_pro_fixture","subscription_cycle");
        var first=fixture.Send("evt_same","invoice.paid",invoice);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.ServiceUnavailable,await fixture.Send("evt_same","invoice.paid",invoice));
        release.SetResult(true);Assert.Equal(HttpStatusCode.OK,await first);
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_same","invoice.paid",invoice));
        Assert.Equal(100,fixture.State.Credits);Assert.Single(fixture.Journal);
        fixture.Factory.Stripe.Verify(s=>s.GetSubscriptionAsync("sub_fixture"),Times.Once);
    }
    [Fact] public async Task FailedAfterCreditCanRetryWithoutDoubleCredit()
    {
        using var fixture=new WebhookFixture();fixture.FailCompleteOnce=true;
        var invoice=Invoice("in_retry","price_pro_fixture","subscription_cycle");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,await fixture.Send("evt_retry","invoice.paid",invoice));
        Assert.Equal(100,fixture.State.Credits);
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_retry","invoice.paid",invoice));
        Assert.Equal(100,fixture.State.Credits);Assert.Single(fixture.Journal);
    }
    [Fact] public async Task NewThenOldSubscriptionCannotRegressPlanStatusOrPeriod()
    {
        using var fixture=new WebhookFixture();
        var newer=Subscription("price_ultra_fixture");newer.Status="active";
        var stamp=DateTime.UtcNow;
        fixture.Factory.Stripe.Setup(s=>s.GetSubscriptionAsync("sub_fixture")).ReturnsAsync(newer);
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_new","customer.subscription.updated",newer,stamp));
        var expected=fixture.State.CurrentPeriodEnd;
        var older=Subscription("price_pro_fixture");older.Status="past_due";
        fixture.Factory.Stripe.Setup(s=>s.GetSubscriptionAsync("sub_fixture")).ReturnsAsync(older);
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_old","customer.subscription.updated",older,stamp.AddMinutes(-1)));
        Assert.Equal("ULTRA",fixture.State.Plan);Assert.Equal("active",fixture.State.SubscriptionStatus);
        Assert.Equal(expected,fixture.State.CurrentPeriodEnd);
    }
    [Fact] public async Task CancellationReconcilesCurrentStateAndPreservesCreditsAtEqualTimestamp()
    {
        using var fixture=new WebhookFixture();fixture.State.Credits=100;
        var stamp=DateTime.UtcNow;
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_new","customer.subscription.updated",Subscription("price_pro_fixture"),stamp));
        var canceled=Subscription("price_pro_fixture");canceled.Status="canceled";
        fixture.Factory.Stripe.Setup(s=>s.GetSubscriptionAsync("sub_fixture")).ReturnsAsync(canceled);
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_deleted","customer.subscription.deleted",canceled,stamp));
        Assert.Null(fixture.State.Plan);Assert.Equal("canceled",fixture.State.SubscriptionStatus);
        Assert.Equal(SubscriptionType.Free,fixture.State.Subscription);Assert.Equal(100,fixture.State.Credits);
        // An old same-second payload is never used as the current state.
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_old_equal","customer.subscription.updated",Subscription("price_pro_fixture"),stamp));
        Assert.Null(fixture.State.Plan);Assert.Equal("canceled",fixture.State.SubscriptionStatus);
    }
    [Theory]
    [InlineData("subscription_update","price_pro_fixture",false)]
    [InlineData("manual","price_pro_fixture",false)]
    [InlineData("subscription_cycle","price_unknown",false)]
    [InlineData("subscription_cycle","price_pro_fixture",true)]
    public async Task NonAllowanceInvoicesCannotGrantCredits(string reason,string price,bool live)
    {
        using var fixture=new WebhookFixture();var invoice=Invoice("in_bad",price,reason);invoice.Livemode=live;
        Assert.Equal(HttpStatusCode.OK,await fixture.Send("evt_nonallowance","invoice.paid",invoice));
        Assert.Equal(0,fixture.State.Credits);
    }
    [Fact] public async Task LiveEventRejectedInStaging()
    {
        using var fixture=new WebhookFixture();
        Assert.Equal(HttpStatusCode.BadRequest,await fixture.Send("evt_live","invoice.paid",Invoice("in_live","price_pro_fixture","subscription_cycle"),live:true));
        Assert.Empty(fixture.Journal);Assert.Equal(0,fixture.State.Credits);
    }
    [Fact] public void MongoSnapshotFilterIncludesAtomicRevisionAndMonotonicTime()
    {
        var f=UserRepository.BillingSnapshotFilter(new User {Id="507f1f77bcf86cd799439011",BillingRevision=4},DateTime.UtcNow);
        var json=f.Render(new RenderArgs<User>(BsonSerializer.SerializerRegistry.GetSerializer<User>(),BsonSerializer.SerializerRegistry)).ToJson();
        Assert.Contains("BillingRevision",json);Assert.Contains("LastSubscriptionEventAt",json);Assert.Contains("$lte",json);
    }
    private static SecurityApiFactory Factory()
    {
        var factory=new SecurityApiFactory();
        foreach(var pair in Config().Where(p=>!p.Key.StartsWith("Jwt:"))) factory.ExtraSettings[pair.Key]=pair.Value!;
        return factory;
    }
    private static Stripe.Subscription Subscription(string price) => new() {
        Object="subscription",Id="sub_fixture",CustomerId="cus_fixture",Status="active",LatestInvoiceId="in_initial",
        Items=new StripeList<SubscriptionItem> {Data=[new SubscriptionItem {Id="si_fixture",Price=new Price {Id=price},Quantity=1,
            CurrentPeriodEnd=DateTime.UtcNow.AddDays(30)}]}
    };
    private static Invoice Invoice(string id,string price,string reason) => new() {
        Object="invoice",Id=id,CustomerId="cus_fixture",Status="paid",AmountPaid=1000,BillingReason=reason,
        Parent=new InvoiceParent {Type="subscription_details",SubscriptionDetails=new InvoiceParentSubscriptionDetails {SubscriptionId="sub_fixture"}},
        Lines=new StripeList<InvoiceLineItem> {HasMore=false,Data=[new InvoiceLineItem {Object="line_item",Id="il_fixture",Quantity=1,
            Pricing=new InvoiceLineItemPricing {Type="price_details",PriceDetails=new InvoiceLineItemPricingPriceDetails {PriceId=price}},
            Parent=new InvoiceLineItemParent {Type="subscription_item_details",SubscriptionItemDetails=new InvoiceLineItemParentSubscriptionItemDetails {Proration=false}}
        }]}
    };
    private sealed class WebhookFixture : IDisposable
    {
        public SecurityApiFactory Factory {get;}=BillingSecurityTests.Factory();
        public User State {get;}=new() {Id="user-a",StripeCustomerId="cus_fixture",Credits=0};
        public HashSet<string> CreditKeys {get;}=[];
        public Dictionary<string,StripeEventRecord> Journal {get;}=[];
        public bool FailCompleteOnce {get;set;}
        private readonly object sync=new();
        private User Snapshot()=>JsonConvert.DeserializeObject<User>(JsonConvert.SerializeObject(State))!;
        public WebhookFixture()
        {
            Factory.UserRepository.Setup(r=>r.GetByIdAsync("user-a")).ReturnsAsync(()=>{lock(sync)return Snapshot();});
            Factory.UserRepository.Setup(r=>r.GetByStripeCustomerIdAsync("cus_fixture")).ReturnsAsync(()=>{lock(sync)return Snapshot();});
            Factory.UserRepository.Setup(r=>r.UpdateBillingSnapshotAsync(It.IsAny<User>(),It.IsAny<DateTime>()))
                .ReturnsAsync((User u,DateTime created)=>{lock(sync) {
                    if(u.BillingRevision!=State.BillingRevision || State.LastSubscriptionEventAt>created)return false;
                    State.Plan=u.Plan;State.SubscriptionStatus=u.SubscriptionStatus;State.Subscription=u.Subscription;
                    State.StripeSubscriptionId=u.StripeSubscriptionId;State.CurrentPeriodEnd=u.CurrentPeriodEnd;
                    State.LastSubscriptionEventAt=created;State.BillingRevision++;return true;
                }});
            Factory.UserRepository.Setup(r=>r.IncrementBillingCreditsOnceAsync("user-a",It.IsAny<int>(),It.IsAny<string>()))
                .ReturnsAsync((string id,int amount,string key)=>{lock(sync){if(!CreditKeys.Add(key))return false;State.Credits+=amount;return true;}});
            Factory.Stripe.Setup(s=>s.GetSubscriptionAsync("sub_fixture")).ReturnsAsync(()=>Subscription("price_pro_fixture"));
            Factory.StripeEvents.Setup(r=>r.TryClaimAsync(It.IsAny<StripeEventRecord>())).ReturnsAsync((StripeEventRecord r)=>{lock(sync) {
                if(Journal.TryGetValue(r.EventId,out var old)) {
                    if(old.Status=="completed")return StripeClaim.Completed;
                    if(old.Status=="processing")return StripeClaim.Busy;
                }
                Journal[r.EventId]=r;return StripeClaim.Acquired;
            }});
            Factory.StripeEvents.Setup(r=>r.CompleteAsync(It.IsAny<string>(),It.IsAny<string>()))
                .Returns((string id,string claim)=>{lock(sync) {
                    if(FailCompleteOnce){FailCompleteOnce=false;return Task.FromException(new Exception("simulated journal failure"));}
                    Journal[id].Status="completed";return Task.CompletedTask;
                }});
            Factory.StripeEvents.Setup(r=>r.FailAsync(It.IsAny<string>(),It.IsAny<string>()))
                .Returns((string id,string claim)=>{lock(sync){Journal[id].Status="failed";return Task.CompletedTask;}});
        }
        public async Task<HttpStatusCode> Send(string id,string type,IHasObject data,DateTime? created=null,bool live=false)
        {
            var ev=new Event {Object="event",Id=id,Type=type,Created=created??DateTime.UtcNow,ApiVersion=StripeConfiguration.ApiVersion,
                Livemode=live,Data=new EventData {Object=data}};
            var json=JsonConvert.SerializeObject(ev);
            var time=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var hash=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(SigningSecret),Encoding.UTF8.GetBytes(time+"."+json))).ToLowerInvariant();
            using var client=Factory.CreateClient();using var request=new HttpRequestMessage(HttpMethod.Post,"/api/stripe/webhook") {
                Content=new StringContent(json,Encoding.UTF8,"application/json")
            };
            request.Headers.Add("Stripe-Signature",$"t={time},v1={hash}");
            return (await client.SendAsync(request)).StatusCode;
        }
        public void Dispose()=>Factory.Dispose();
    }
}
