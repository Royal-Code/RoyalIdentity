window.addEventListener("load", function () {
    var a = document.querySelector("a.post-logout-redirect-uri.automatic-redirect");
    if (a) {
        window.location = a.href;
    }
});
