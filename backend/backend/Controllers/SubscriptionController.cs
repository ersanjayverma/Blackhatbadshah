using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using backend.Services;
using shared.Dto;

namespace backend.Controllers;

[Route("api/subscriptions")]
public sealed class SubscriptionController : BaseApiController
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IPlanEnforcementService _planEnforcement;
    private readonly IRazorpayService _razorpay;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        ISubscriptionService subscriptionService,
        IPlanEnforcementService planEnforcement,
        IRazorpayService razorpay,
        ILogger<SubscriptionController> logger)
    {
        _subscriptionService = subscriptionService;
        _planEnforcement = planEnforcement;
        _razorpay = razorpay;
        _logger = logger;
    }

    [HttpOptions]
    [AllowAnonymous]
    public IActionResult Options() => NoContent();

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<List<PlanDto>>> GetPlans()
    {
        // userId optional (for personalization)
        var userId = User.FindFirstValue("sub");

        var plans = await _subscriptionService.GetAvailablePlansAsync(userId);
        return Ok(plans);
    }

    // -------------------------------------------------
    // AUTHENTICATED
    // -------------------------------------------------
    [Authorize]
    [HttpGet("current")]
    public async Task<ActionResult<UserSubscriptionDto>> GetCurrentSubscription()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var subscription =
            await _subscriptionService.GetUserSubscriptionAsync(userId);

        return Ok(subscription);
    }

    [Authorize]
    [HttpPost("checkout")]
    public async Task<ActionResult<RazorpayCheckoutDto>> InitiateCheckout(
        [FromBody] CreateSubscriptionRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            _logger.LogInformation(
                "Initiating checkout for user {UserId}, plan {PlanId}, billing {BillingPeriod}",
                userId, request.PlanId, request.BillingPeriod);

            var checkout =
                await _subscriptionService.InitiateSubscriptionAsync(userId, request);

            return Ok(checkout);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Checkout failed for user {UserId}: {Message}", userId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkout error for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred during checkout. Please try again." });
        }
    }

    [Authorize]
    [HttpPost("verify")]
    public async Task<ActionResult<UserSubscriptionDto>> VerifyPayment(
        [FromBody] VerifyPaymentRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            _logger.LogInformation(
                "Verifying payment for user {UserId}, subscription {SubscriptionId}",
                userId, request.RazorpaySubscriptionId);

            var subscription =
                await _subscriptionService.ActivateSubscriptionAsync(userId, request);

            return Ok(subscription);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Payment verification failed for user {UserId}: {Message}", userId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment verification error for user {UserId}", userId);
            return StatusCode(500, new { error = "Payment verification failed. Please contact support." });
        }
    }

    [Authorize]
    [HttpPost("cancel")]
    public async Task<IActionResult> CancelSubscription()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            _logger.LogInformation("Cancelling subscription for user {UserId}", userId);

            await _subscriptionService.CancelSubscriptionAsync(userId);

            return Ok(new
            {
                message =
                    "Subscription cancelled. You will retain access until the end of the billing period."
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cancel subscription failed for user {UserId}: {Message}", userId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancel subscription error for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to cancel subscription. Please try again or contact support." });
        }
    }

    [Authorize]
    [HttpGet("usage")]
    public async Task<ActionResult<UsageStatusDto>> GetUsageStatus()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var usage =
            await _subscriptionService.GetUsageStatusAsync(userId);

        return Ok(usage);
    }

    [Authorize]
    [HttpGet("payments")]
    public async Task<ActionResult<List<PaymentHistoryDto>>> GetPaymentHistory()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var payments =
            await _subscriptionService.GetPaymentHistoryAsync(userId);

        return Ok(payments);
    }

    // -------------------------------------------------
    // PAY AS YOU GO - ONE-TIME CREDIT PURCHASE
    // -------------------------------------------------
    [Authorize]
    [HttpPost("pay-as-you-go/create")]
    public async Task<ActionResult<PayAsYouGoOrderDto>> CreatePayAsYouGoOrder([FromBody] CreatePayAsYouGoRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            var email = User.FindFirst("email")?.Value ?? "";
            var phone = User.FindFirst("phone")?.Value;

            _logger.LogInformation(
                "Creating PayAsYouGo order for user {UserId}, creditType {CreditType}",
                userId, request.CreditType);

            var (orderId, key) = await _razorpay.CreatePayAsYouGoOrderAsync(userId, request.CreditType);

            return Ok(new PayAsYouGoOrderDto
            {
                OrderId = orderId,
                Key = key,
                Amount = 50000, // ₹500 in paise
                Currency = "INR",
                Name = "Blackhatbadshah",
                Description = request.CreditType == "Open" ? "30 Open Model Credits" : "15 Pro Model Credits",
                Email = email,
                Phone = phone,
                CreditType = request.CreditType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayAsYouGo order creation error for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to create order. Please try again." });
        }
    }

    [Authorize]
    [HttpPost("pay-as-you-go/verify")]
    public async Task<ActionResult<PayAsYouGoResultDto>> VerifyPayAsYouGo([FromBody] PayAsYouGoVerifyRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            _logger.LogInformation(
                "Verifying PayAsYouGo payment for user {UserId}, payment {PaymentId}, order {OrderId}",
                userId, request.RazorpayPaymentId, request.RazorpayOrderId);

            // Verify payment signature using order-based verification
            var isValid = _razorpay.VerifyOrderPaymentSignature(
                request.RazorpayPaymentId,
                request.RazorpayOrderId,
                request.RazorpaySignature);

            if (!isValid)
            {
                _logger.LogWarning("Invalid PayAsYouGo signature for user {UserId}", userId);
                return BadRequest(new { error = "Invalid payment signature" });
            }

            // Add credits based on user choice: 15 pro OR 30 open for ₹500
            int proCredits = 0;
            int openCredits = 0;
            string creditTypeMessage;

            if (request.CreditType?.Equals("Open", StringComparison.OrdinalIgnoreCase) == true)
            {
                openCredits = 30;
                creditTypeMessage = "30 open model credits";
            }
            else
            {
                proCredits = 15;
                creditTypeMessage = "15 pro model credits";
            }

            await _planEnforcement.AddPurchasedCreditsAsync(userId, proCredits, openCredits);

            _logger.LogInformation(
                "PayAsYouGo credits added for user {UserId}: {ProCredits} pro, {OpenCredits} open",
                userId, proCredits, openCredits);

            return Ok(new PayAsYouGoResultDto
            {
                Message = $"Payment verified! {creditTypeMessage} have been added to your account.",
                ProCredits = proCredits,
                OpenCredits = openCredits
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayAsYouGo verification error for user {UserId}", userId);
            return StatusCode(500, new { error = "Payment verification failed. Please contact support." });
        }
    }
}
