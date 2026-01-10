using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using backend.Services;
using shared.Dto;

namespace backend.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub") ??
        throw new UnauthorizedAccessException();

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<List<PlanDto>>> GetPlans()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var plans = await _subscriptionService.GetAvailablePlansAsync(userId);
        return Ok(plans);
    }

    [HttpGet("current")]
    public async Task<ActionResult<UserSubscriptionDto>> GetCurrentSubscription()
    {
        var userId = GetUserId();
        var subscription = await _subscriptionService.GetUserSubscriptionAsync(userId);
        return Ok(subscription);
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<RazorpayCheckoutDto>> InitiateCheckout([FromBody] CreateSubscriptionRequest request)
    {
        try
        {
            var userId = GetUserId();
            var checkout = await _subscriptionService.InitiateSubscriptionAsync(userId, request);
            return Ok(checkout);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("verify")]
    public async Task<ActionResult<UserSubscriptionDto>> VerifyPayment([FromBody] VerifyPaymentRequest request)
    {
        try
        {
            var userId = GetUserId();
            var subscription = await _subscriptionService.ActivateSubscriptionAsync(userId, request);
            return Ok(subscription);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> CancelSubscription()
    {
        try
        {
            var userId = GetUserId();
            await _subscriptionService.CancelSubscriptionAsync(userId);
            return Ok(new { message = "Subscription cancelled. You will retain access until the end of the billing period." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("usage")]
    public async Task<ActionResult<UsageStatusDto>> GetUsageStatus()
    {
        var userId = GetUserId();
        var usage = await _subscriptionService.GetUsageStatusAsync(userId);
        return Ok(usage);
    }

    [HttpGet("payments")]
    public async Task<ActionResult<List<PaymentHistoryDto>>> GetPaymentHistory()
    {
        var userId = GetUserId();
        var payments = await _subscriptionService.GetPaymentHistoryAsync(userId);
        return Ok(payments);
    }
}
