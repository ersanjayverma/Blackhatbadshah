-- Update Razorpay Plan IDs
-- Run this script after creating subscription plans in Razorpay Dashboard
-- Replace the placeholder values with actual Razorpay Plan IDs (format: plan_XXXXXXXXXXXX)

-- Pro Plan
UPDATE SubscriptionPlans
SET
    RazorpayPlanIdMonthly = 'plan_REPLACE_WITH_PRO_MONTHLY_ID',
    RazorpayPlanIdYearly = 'plan_REPLACE_WITH_PRO_YEARLY_ID',
    UpdatedAt = UTC_TIMESTAMP()
WHERE Name = 'Pro';

-- Enterprise Plan
UPDATE SubscriptionPlans
SET
    RazorpayPlanIdMonthly = 'plan_REPLACE_WITH_ENTERPRISE_MONTHLY_ID',
    RazorpayPlanIdYearly = 'plan_REPLACE_WITH_ENTERPRISE_YEARLY_ID',
    UpdatedAt = UTC_TIMESTAMP()
WHERE Name = 'Enterprise';

-- Verify the updates
SELECT Name, RazorpayPlanIdMonthly, RazorpayPlanIdYearly, PriceMonthly, PriceYearly
FROM SubscriptionPlans
ORDER BY SortOrder;
