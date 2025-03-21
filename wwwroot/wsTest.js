window.blazorWebSocketFix = {
    init: function() {
        console.log("Initializing Blazor WebSocket diagnostics");
        
        // Track WebSocket connection state
        let isReconnecting = false;

        // Listen for Blazor connection events
        window.addEventListener('blazor:start', () => {
            console.log("✅ Blazor circuit started");
            document.body.classList.remove("disconnected");
            document.body.classList.add("connected");
        });

        window.addEventListener('blazor:disconnect', () => {
            console.log("⛔ Blazor circuit disconnected");
            document.body.classList.remove("connected");
            document.body.classList.add("disconnected");
            
            if (!isReconnecting) {
                isReconnecting = true;
                console.log("Attempting to reconnect in 3 seconds...");
                setTimeout(() => {
                    console.log("Reloading page to reestablish connection");
                    location.reload();
                }, 3000);
            }
        });

        // Monitor WebSocket connections
        const originalWebSocket = window.WebSocket;
        window.WebSocket = function(url, protocols) {
            console.log("🔌 WebSocket connecting to:", url);
            const ws = new originalWebSocket(url, protocols);
            
            ws.addEventListener('open', () => {
                console.log("🔌 WebSocket connection established");
            });
            
            ws.addEventListener('error', (error) => {
                console.error("🔌 WebSocket error:", error);
            });
            
            ws.addEventListener('close', (event) => {
                console.log(`🔌 WebSocket closed: code=${event.code}, reason=${event.reason}`);
            });
            
            return ws;
        };
    },
    
    forceReconnect: function() {
        console.log("Forcing reconnection...");
        location.reload();
    }
};