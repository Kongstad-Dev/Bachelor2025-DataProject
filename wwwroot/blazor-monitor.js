console.log("Blazor Monitor loaded");

(function() {
    let blazorStarted = false;

    // Create status indicator
    const indicator = document.createElement('div');
    indicator.id = 'blazor-status';
    indicator.style.position = 'fixed';
    indicator.style.top = '40px';
    indicator.style.right = '10px';
    indicator.style.padding = '5px 10px';
    indicator.style.background = 'gray';
    indicator.style.color = 'white';
    indicator.style.zIndex = '99999';
    indicator.style.fontFamily = 'sans-serif';
    indicator.style.fontSize = '12px';
    indicator.style.borderRadius = '3px';
    indicator.innerText = 'Blazor: Checking...';
    document.body.appendChild(indicator);
    
    // Add a reconnect button
    const reconnectBtn = document.createElement('button');
    reconnectBtn.innerText = 'Reconnect';
    reconnectBtn.style.marginLeft = '5px';
    reconnectBtn.onclick = function() {
        console.log("Manual reconnect requested");
        location.reload();
    };
    indicator.appendChild(reconnectBtn);
    
    // Monitor Blazor events
    window.addEventListener('blazor:start', function() {
        console.log("✅ Blazor start event received");
        blazorStarted = true;
        indicator.style.background = 'green';
        indicator.innerText = 'Blazor: Connected';
        indicator.appendChild(reconnectBtn);
    });
    
    window.addEventListener('blazor:disconnect', function() {
        console.log("❌ Blazor disconnect event received");
        indicator.style.background = 'red';
        indicator.innerText = 'Blazor: Disconnected';
        indicator.appendChild(reconnectBtn);
    });

    // Fallback detection if events don't fire
    setTimeout(function checkBlazor() {
        // Check if DotNet is available as a way to detect Blazor
        if (!blazorStarted && window.DotNet) {
            console.log("✅ Detected Blazor via DotNet object");
            indicator.style.background = 'green';
            indicator.innerText = 'Blazor: Connected (detected)';
            indicator.appendChild(reconnectBtn);
            blazorStarted = true;
        } else if (!blazorStarted) {
            indicator.innerText = 'Blazor: Not Detected';
            console.log("Checking Blazor status again in 2 seconds...");
            setTimeout(checkBlazor, 2000);
        }
    }, 2000);
    
    // Create a debug button
    const debugBtn = document.createElement('button');
    debugBtn.innerText = 'Debug Blazor';
    debugBtn.style.position = 'fixed';
    debugBtn.style.bottom = '10px';
    debugBtn.style.right = '10px';
    debugBtn.style.padding = '5px 10px';
    debugBtn.style.zIndex = '99999';
    debugBtn.onclick = function() {
        console.log("DotNet available:", !!window.DotNet);
        console.log("Blazor available:", !!window.Blazor);
        if (window.Blazor) {
            for (const key in window.Blazor) {
                console.log(`- Blazor.${key} exists`);
            }
        }
    };
    document.body.appendChild(debugBtn);
})();