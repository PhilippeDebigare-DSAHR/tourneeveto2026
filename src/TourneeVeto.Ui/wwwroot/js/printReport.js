const styleAttribute = "data-print-report";

// Masque, le temps de l'impression, la navigation hors du rapport. Aucun contenu HTML n'est reçu de l'appelant.
function installPrintStyle() {
    const style = document.createElement("style");
    style.setAttribute(styleAttribute, "");
    style.textContent =
        "@media print { nav, aside, [role='navigation'], body > header, body > footer { display: none !important; } }";
    document.head.appendChild(style);
    return style;
}

async function waitForImages() {
    const images = Array.from(document.querySelectorAll("img[data-report-photo]"));
    await Promise.all(images.map(async image => {
        try {
            await image.decode();
        } catch {
            throw new Error("La photo n'a pas pu être préparée pour l'impression.");
        }
    }));
}

export async function printReport() {
    if (typeof window.print !== "function") {
        throw new Error("L'impression n'est pas disponible dans ce navigateur.");
    }

    const style = installPrintStyle();
    try {
        await waitForImages();
        window.print();
    } finally {
        style.remove();
    }
}
