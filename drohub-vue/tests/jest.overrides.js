let error = console.error
let warn = console.warn

console.error = function (message) {
    // error.apply(console, arguments) // keep default behaviour
    throw (message instanceof Error ? message : new Error(message))
}

//For GalleryTimeLine. Needs to be done in a different js file thus why it is not in the spec file.
//https://jestjs.io/docs/en/manual-mocks#mocking-methods-which-are-not-implemented-in-jsdom
Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: jest.fn().mockImplementation(query => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: jest.fn(), // Deprecated
        removeListener: jest.fn(), // Deprecated
        addEventListener: jest.fn(),
        removeEventListener: jest.fn(),
        dispatchEvent: jest.fn(),
    }))
});
