class MessageCounter {
    constructor() {
        this.counterElement = document.getElementById('message-counter');
        this.unreadCount = 0;
        this.initializeSignalR();
        this.loadInitialCount();
    }

    initializeSignalR() {
        // Wait for SignalR connection to be available
        const waitForConnection = () => {
            if (typeof connection !== 'undefined' && connection.state === signalR.HubConnectionState.Connected) {
                connection.on("UpdateNotificationCount", (count) => {
                    this.updateCounter(count);
                });

                connection.on("TotalUnreadCount", (count) => {
                    this.updateCounter(count);
                });

                // Request initial count
                connection.invoke("GetTotalUnreadCount").catch(err => {
                    console.error('Error getting unread count:', err);
                });
            } else {
                // Retry after 500ms if connection isn't ready
                setTimeout(waitForConnection, 500);
            }
        };

        waitForConnection();
    }

    async loadInitialCount() {
        try {
            const response = await fetch('/api/messages/unread-count');
            if (response.ok) {
                const data = await response.json();
                this.updateCounter(data.count || 0);
            }
        } catch (error) {
            console.error('Error loading unread message count:', error);
        }
    }

    updateCounter(count) {
        this.unreadCount = count;

        if (this.counterElement) {
            this.counterElement.textContent = count;

            if (count > 0) {
                this.counterElement.style.display = 'inline-flex';
                // Add animation for new messages
                this.counterElement.classList.add('animate-pulse');
                setTimeout(() => {
                    this.counterElement.classList.remove('animate-pulse');
                }, 1000);
            } else {
                this.counterElement.style.display = 'none';
            }
        }
    }

    // Method to reset counter when user opens chat
    resetCounter() {
        // Don't reset here - let SignalR handle the update after marking as read
    }
}

// Initialize message counter when DOM is loaded
document.addEventListener('DOMContentLoaded', function () {
    window.messageCounter = new MessageCounter();

    // When user clicks on chat link, the counter will be updated via SignalR
    // when they actually read the messages
});