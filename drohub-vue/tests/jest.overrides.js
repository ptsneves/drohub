let error = console.error
let warn = console.warn

console.error = function (message) {
    // error.apply(console, arguments) // keep default behaviour
    throw (message instanceof Error ? message : new Error(message))
}
