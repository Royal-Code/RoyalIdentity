export function onLoad() {

    console.log('Loaded');

    let el = document.getElementById('consent-redirect');
    if (el === null || el === undefined) {
        return;
    }

    // get data-returnUrl attribute
    let returnUrl = el.getAttribute('data-returnUrl');

    // redirect to returnUrl
    setTimeout(function () {

        console.log('redirecting...');

        window.location.href = returnUrl;
    }, 2000);
}

export function onUpdate() {
    console.log('Updated');
}

export function onDispose() {
    console.log('Disposed');
}