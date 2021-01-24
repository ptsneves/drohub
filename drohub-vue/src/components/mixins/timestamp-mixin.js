

export default {
    methods: {
        _MIXIN_TIMESTAMP_convertUnixToLocalTime(unix_timestamp) {
            // https://stackoverflow.com/a/847196/227990
            const date = new Date(unix_timestamp);
            return date.toLocaleString();
        },

        _MIXIN_TIMESTAMP_convertUnixToLocalDate(unix_timestamp) {
            const date = new Date(unix_timestamp);
            return date.toLocaleDateString();
        },
        _MIXIN_TIMESTAMP_getTimeText(unix_time, unix_time_stamp_units, show_only_date) {
            unix_time = parseInt(unix_time, 10);
            if (unix_time_stamp_units === 'us') {
                unix_time /= 1000;
            }
            else if (unix_time_stamp_units === 's') {
                unix_time *= 1000;
            }

            return show_only_date
                ? this._MIXIN_TIMESTAMP_convertUnixToLocalDate(unix_time)
                : this._MIXIN_TIMESTAMP_convertUnixToLocalTime(unix_time);
        },
    },
}
