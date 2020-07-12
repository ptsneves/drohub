module.exports = {
    lintOnSave: false,
    publicPath: '/vue/',
    outputDir: '../wwwroot/vue/',
    filenameHashing: false,
    configureWebpack: {
        optimization: {
            splitChunks: false
        },
        resolve: {
            alias: {
                'vue$': 'vue/dist/vue.esm.js'
            }
        },
    },
    pages: {
      Gallery: {
        entry: 'src/Gallery/gallery.js'
      }

    }
}
