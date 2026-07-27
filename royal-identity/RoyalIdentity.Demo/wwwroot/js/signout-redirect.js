window.addEventListener("load", function () {
    const link = document.querySelector("a.post-logout-redirect-uri.automatic-redirect");
    if (link) {
        window.location = link.href;
    }
});
