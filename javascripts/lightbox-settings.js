lightbox.option({
    'fadeDuration': 100,
    'imageFadeDuration': 100,
    'resizeDuration': 100
})

class PopupImage extends HTMLElement {
    constructor() {
        super();
    }
    connectedCallback() {
        var src = this.getAttribute("src");
        var width = this.getAttribute("width");
        var alt = this.getAttribute("alt");
        if (alt == "")
            alt = src;

        this.innerHTML = `<a href="${src}" data-lightbox="${alt}" data-title="" data-alt="${alt}"><img src="${src}" width="${width}"></a>`;
    }
}
customElements.define("popup-img", PopupImage);
