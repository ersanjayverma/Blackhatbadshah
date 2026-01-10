window.openRazorpayCheckout = function(options, dotNetRef) {
    var rzp = new Razorpay({
        key: options.key,
        subscription_id: options.subscription_id,
        name: options.name,
        description: options.description,
        prefill: options.prefill,
        theme: {
            color: "#6366f1"
        },
        handler: function(response) {
            dotNetRef.invokeMethodAsync('OnPaymentSuccess',
                response.razorpay_payment_id,
                response.razorpay_subscription_id,
                response.razorpay_signature);
        },
        modal: {
            ondismiss: function() {
                console.log("Checkout modal closed");
            }
        }
    });

    rzp.on('payment.failed', function(response) {
        dotNetRef.invokeMethodAsync('OnPaymentFailed',
            response.error.description || response.error.reason || 'Payment failed');
    });

    rzp.open();
};
