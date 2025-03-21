console.log("WebSocket test script loaded");

(function() {
    // Create test button
    const wsTestButton = document.createElement('button');
    wsTestButton.innerText = 'Test WebSockets';
    wsTestButton.style.position = 'fixed';
    wsTestButton.style.bottom = '50px';
    wsTestButton.style.right = '10px';
    wsTestButton.style.padding = '5px 10px';
    wsTestButton.style.zIndex = '99999';
    
    wsTestButton.onclick = function() {
        // Create a simple WebSocket
        try {
            const ws = new WebSocket(`ws://${window.location.host}/ws-test`);
            
            ws.onopen = () => {
                console.log("Test WebSocket opened successfully");
                alert("WebSocket connection works!");
            };
            
            ws.onerror = (error) => {
                console.error("Test WebSocket error", error);
                alert("WebSocket error occurred");
            };
            
            ws.onclose = (event) => {
                console.log("Test WebSocket closed", event.code, event.reason);
                alert(`WebSocket closed: ${event.code} ${event.reason}`);
            };
        } catch (error) {
            console.error("Failed to create WebSocket", error);
            alert(`WebSocket creation failed: ${error.message}`);
        }
    };
    
    document.body.appendChild(wsTestButton);
})();