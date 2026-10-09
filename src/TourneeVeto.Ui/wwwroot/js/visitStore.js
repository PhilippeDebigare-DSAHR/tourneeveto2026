const databaseName = "tourneeveto";
const schemaVersion = 2;

function invalid(message) {
    return new DOMException(message, "DataError");
}

function requireId(id) {
    if (typeof id !== "string" ||
        !/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/.test(id) ||
        id === "00000000-0000-0000-0000-000000000000") {
        throw invalid("Identifiant invalide.");
    }
}

function requirePhoto(id, contentType, data) {
    requireId(id);
    if (typeof contentType !== "string" || !/^image\/.+$/i.test(contentType) ||
        !(data instanceof Uint8Array) || data.length === 0) {
        throw invalid("La photo doit contenir des octets et un type MIME d'image.");
    }
}

function requireDate(value) {
    if (typeof value !== "string" || !/^\d{4}-\d{2}-\d{2}$/.test(value)) {
        throw invalid("Date ISO invalide.");
    }
    const [year, month, day] = value.split("-").map(Number);
    const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
    const days = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    if (year < 1 || month < 1 || month > 12 || day < 1 || day > days[month - 1]) {
        throw invalid("Date ISO invalide.");
    }
}

function requireHerd(herd) {
    if (!herd || !Array.isArray(herd.locations) || !Array.isArray(herd.cows)) {
        throw invalid("Document de troupeau invalide.");
    }
    const locations = new Set();
    for (const location of herd.locations) {
        requireId(location?.id);
        if (locations.has(location.id) || typeof location.name !== "string" ||
            typeof location.city !== "string" || !Number.isInteger(location.animalCount) ||
            location.animalCount < 0) {
            throw invalid("Élevage invalide ou dupliqué.");
        }
        locations.add(location.id);
    }
    const cows = new Map();
    for (const cow of herd.cows) {
        requireId(cow?.id);
        if (cows.has(cow.id) || !locations.has(cow.locationId) ||
            typeof cow.name !== "string" || !Number.isInteger(cow.lactation) ||
            cow.lactation < 0 || !Number.isInteger(cow.reproductionStatus) ||
            cow.reproductionStatus < 0 || cow.reproductionStatus > 4 ||
            (cow.lastCcs !== null && (!Number.isInteger(cow.lastCcs) || cow.lastCcs < 0))) {
            throw invalid("Vache invalide, dupliquée ou sans élevage.");
        }
        requireDate(cow.birthDate);
        if (cow.lastCalving !== null) {
            requireDate(cow.lastCalving);
        }
        if (cow.lastInsemination !== null) {
            requireDate(cow.lastInsemination);
        }
        cows.set(cow.id, cow.locationId);
    }
    return { locations, cows };
}

function requireVisit(visit) {
    requireId(visit?.id);
    requireId(visit.farmId);
    requireDate(visit.date);
    if (typeof visit.cause !== "string" || typeof visit.notes !== "string" ||
        !Object.hasOwn(visit, "photoId") ||
        (Object.hasOwn(visit, "actions") && !Array.isArray(visit.actions))) {
        throw invalid("Document de visite invalide.");
    }
    if (visit.photoId !== null) {
        requireId(visit.photoId);
    }
    const ids = new Set();
    for (const action of visit.actions ?? []) {
        requireId(action?.id);
        requireId(action.cowId);
        requireDate(action.date);
        if (ids.has(action.id) || !Number.isInteger(action.type) || action.type < 0 || action.type > 4 ||
            typeof action.isCompleted !== "boolean" || typeof action.notes !== "string") {
            throw invalid("Action invalide ou dupliquée.");
        }
        ids.add(action.id);
    }
}

function requireSnapshot(herd, visits) {
    const { locations, cows } = requireHerd(herd);
    if (locations.size === 0 || cows.size === 0 || !Array.isArray(visits)) {
        throw invalid("Le jeu de démonstration est incomplet.");
    }
    const ids = new Set([...locations, ...cows.keys()]);
    for (const visit of visits) {
        requireVisit(visit);
        if (ids.has(visit.id) || !locations.has(visit.farmId)) {
            throw invalid("Visite dupliquée ou sans élevage.");
        }
        ids.add(visit.id);
        for (const action of visit.actions ?? []) {
            if (ids.has(action.id) || cows.get(action.cowId) !== visit.farmId) {
                throw invalid("Action dupliquée ou rattachée à un autre élevage.");
            }
            ids.add(action.id);
        }
    }
}

function openDatabase() {
    return new Promise((resolve, reject) => {
        if (!globalThis.indexedDB) {
            reject(new DOMException("IndexedDB indisponible.", "NotSupportedError"));
            return;
        }

        const request = indexedDB.open(databaseName, schemaVersion);
        let blocked = false;
        request.onblocked = () => {
            blocked = true;
            reject(new DOMException("Fermez les autres onglets avant de réessayer.", "UpgradeBlockedError"));
        };
        request.onupgradeneeded = event => {
            if (blocked) {
                request.transaction.abort();
                return;
            }

            const db = request.result;
            if (event.oldVersion < 1) {
                db.createObjectStore("visits", { keyPath: "id" });
                db.createObjectStore("herds", { keyPath: "id" });
            }
            if (event.oldVersion < 2) {
                db.createObjectStore("photos", { keyPath: "id" });
            }
        };
        request.onerror = () => reject(request.error);
        request.onsuccess = () => {
            const db = request.result;
            db.onversionchange = () => db.close();
            if (blocked) {
                db.close();
            } else {
                resolve(db);
            }
        };
    });
}

function requestResult(request) {
    return new Promise((resolve, reject) => {
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

function transactionResult(db, stores, mode, work) {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(stores, mode);
        let result;
        let failure;
        transaction.oncomplete = () => resolve(result);
        transaction.onabort = () => reject(failure ?? transaction.error ??
            new DOMException("Transaction annulée.", "AbortError"));

        // work n'attend que des requêtes IndexedDB, jamais un timer ou le réseau.
        Promise.resolve().then(() => work(transaction)).then(value => {
            result = value;
        }).catch(error => {
            failure = error;
            try {
                transaction.abort();
            } catch (abortError) {
                if (abortError.name !== "InvalidStateError") {
                    reject(abortError);
                    return;
                }
                reject(error);
            }
        });
    });
}

function failureResult(error) {
    const codes = {
        QuotaExceededError: "quota",
        SecurityError: "unavailable",
        NotAllowedError: "unavailable",
        NotSupportedError: "unavailable",
        UpgradeBlockedError: "blocked",
        VersionError: "version",
        DataError: "invalid",
        SyntaxError: "invalid"
    };
    return { value: null, error: {
        code: codes[error?.name] ?? "unknown",
        detail: `${error?.name ?? "Error"}: ${error?.message ?? String(error)}`
    } };
}

async function execute(stores, mode, work) {
    let db;
    try {
        db = await openDatabase();
        return { value: await transactionResult(db, stores, mode, work), error: null };
    } catch (error) {
        // Frontière d'interop : chaque échec devient une erreur structurée, jamais un succès.
        return failureResult(error);
    } finally {
        db?.close();
    }
}

async function saveVisit(transaction, visit, data, contentType) {
    requireVisit(visit);

    const visits = transaction.objectStore("visits");
    const photos = transaction.objectStore("photos");
    const previous = await requestResult(visits.get(visit.id));
    if (visit.photoId !== null) {
        requireId(visit.photoId);
        const existing = await requestResult(photos.get(visit.photoId));
        if (existing && existing.visitId !== visit.id) {
            throw invalid("La photo appartient à une autre visite.");
        }

        if (data != null) {
            requirePhoto(visit.photoId, contentType, data);
            await requestResult(photos.put({
                id: visit.photoId, visitId: visit.id, contentType, data: new Uint8Array(data)
            }));
        } else if (!existing) {
            throw invalid("La photo référencée est absente.");
        }
    } else if (data != null) {
        throw invalid("La photo fournie nécessite un PhotoId.");
    }

    if (previous?.photoId && previous.photoId !== visit.photoId) {
        await requestResult(photos.delete(previous.photoId));
    }
    await requestResult(visits.put(visit));
    return true;
}

export async function getAll() {
    return JSON.stringify(await execute(["visits"], "readonly",
        async transaction => {
            const visits = await requestResult(transaction.objectStore("visits").getAll());
            visits.forEach(requireVisit);
            return visits;
        }));
}

export async function get(id) {
    return JSON.stringify(await execute(["visits"], "readonly", async transaction => {
        requireId(id);
        const visit = await requestResult(transaction.objectStore("visits").get(id));
        if (visit) {
            requireVisit(visit);
        }
        return visit ?? null;
    }));
}

export async function save(json, data = null, contentType = null) {
    return JSON.stringify(await execute(["visits", "photos"], "readwrite",
        transaction => saveVisit(transaction, JSON.parse(json), data, contentType)));
}

export async function deleteVisit(id) {
    return JSON.stringify(await execute(["visits", "photos"], "readwrite", async transaction => {
        requireId(id);
        const visits = transaction.objectStore("visits");
        const visit = await requestResult(visits.get(id));
        if (visit?.photoId) {
            await requestResult(transaction.objectStore("photos").delete(visit.photoId));
        }
        await requestResult(visits.delete(id));
        return true;
    }));
}

export async function getHerd() {
    return JSON.stringify(await execute(["herds"], "readonly", async transaction => {
        const herd = await requestResult(transaction.objectStore("herds").get("current"));
        if (herd) {
            requireHerd(herd);
        }
        return herd ? { locations: herd.locations, cows: herd.cows } : null;
    }));
}

export async function saveHerd(json) {
    return JSON.stringify(await execute(["herds"], "readwrite", async transaction => {
        const herd = JSON.parse(json);
        requireHerd(herd);
        await requestResult(transaction.objectStore("herds").put({
            id: "current", locations: herd.locations, cows: herd.cows
        }));
        return true;
    }));
}

export async function initializeDemo(json) {
    return JSON.stringify(await execute(["herds", "visits"], "readwrite", async transaction => {
        const herds = transaction.objectStore("herds");
        const visits = transaction.objectStore("visits");
        const marker = await requestResult(herds.get("demo-initialization"));
        const herd = await requestResult(herds.get("current"));
        const existingVisits = await requestResult(visits.getAll());
        if (marker || herd) {
            requireSnapshot(herd, existingVisits);
            if (marker) {
                if (marker.format !== 1 || typeof marker.adopted !== "boolean") {
                    throw invalid("Marqueur d'initialisation illisible.");
                }
                if (!marker.adopted) {
                    requireDate(marker.referenceDate);
                    if (!Number.isInteger(marker.seed)) {
                        throw invalid("Marqueur d'initialisation illisible.");
                    }
                }
                return true;
            }
            // Adopter le socle antérieur sans compléter ni écraser les saisies.
            await requestResult(herds.put({ id: "demo-initialization", format: 1, adopted: true }));
            return true;
        }
        if (existingVisits.length !== 0) {
            throw invalid("Des visites existent sans troupeau. Aucune réinitialisation n'est effectuée.");
        }

        const seed = JSON.parse(json);
        requireDate(seed?.referenceDate);
        if (!Number.isInteger(seed.seed) || !Array.isArray(seed.visits) || seed.visits.length === 0) {
            throw invalid("Jeu de démonstration invalide.");
        }
        requireSnapshot(seed.herd, seed.visits);
        if (seed.visits.some(visit => visit.photoId !== null)) {
            throw invalid("Le jeu initial ne doit pas référencer de photos.");
        }
        await requestResult(herds.put({ id: "current",
            locations: seed.herd.locations, cows: seed.herd.cows }));
        for (const visit of seed.visits) {
            await requestResult(visits.add(visit));
        }
        await requestResult(herds.add({ id: "demo-initialization", format: 1,
            adopted: false, referenceDate: seed.referenceDate, seed: seed.seed }));
        return true;
    }));
}

export async function getPhoto(id) {
    const result = await execute(["photos"], "readonly", async transaction => {
        requireId(id);
        return await requestResult(transaction.objectStore("photos").get(id)) ?? null;
    });
    if (result.error || result.value === null) {
        return { result: JSON.stringify(result), data: null };
    }

    const photo = result.value;
    return {
        result: JSON.stringify({ value: { id: photo.id, contentType: photo.contentType }, error: null }),
        data: photo.data
    };
}

export async function savePhoto(visitId, photoId, contentType, data) {
    return JSON.stringify(await execute(["visits", "photos"], "readwrite", async transaction => {
        requireId(visitId);
        requirePhoto(photoId, contentType, data);
        const visit = await requestResult(transaction.objectStore("visits").get(visitId));
        if (!visit) {
            throw invalid("La visite est absente.");
        }
        return saveVisit(transaction, { ...visit, photoId }, data, contentType);
    }));
}

export async function deletePhoto(visitId) {
    return JSON.stringify(await execute(["visits", "photos"], "readwrite", async transaction => {
        requireId(visitId);
        const visit = await requestResult(transaction.objectStore("visits").get(visitId));
        if (!visit) {
            throw invalid("La visite est absente.");
        }
        return saveVisit(transaction, { ...visit, photoId: null }, null, null);
    }));
}
