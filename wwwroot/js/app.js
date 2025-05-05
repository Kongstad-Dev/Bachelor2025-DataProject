window.scrollToElement = function(elementClass) {
    const element = document.querySelector('.' + elementClass);
    if (element) {
        element.scrollIntoView({ 
            behavior: 'smooth', 
            block: 'start'
        });
    }
};