<template>
    <span class='time'>
        {{ getTimeText }}
    </span>
</template>

<script>
function _convertUnixToLocalTime(unix_timestamp) {
    // https://stackoverflow.com/a/847196/227990
    const date = new Date(unix_timestamp);
    return date.toLocaleString();
}

function _convertUnixToLocalDate(unix_timestamp) {
    const date = new Date(unix_timestamp);
    return date.toLocaleDateString();
}

export default {
    name: 'TimeLabel',
    props: {
        unixTimeStamp: {
            type: String,
            required: true,
        },
        showOnlyDate: {
            type: Boolean,
            default: false,
        },
        unixTimeStampUnits: {
            type: String,
            required: true,
            validator(value) {
                return ['us', 'ms', 's'].indexOf(value) !== -1;
            },
        },
    },
    computed: {
        getTimeText() {
            let unix_time_in_ms = parseInt(this.unixTimeStamp, 10);

            if (this.unixTimeStampUnits === 'us') {
                unix_time_in_ms /= 1000;
            }
            else if (this.unixTimeStampUnits === 's') {
                unix_time_in_ms *= 1000;
            }

            return this.showOnlyDate
                ? _convertUnixToLocalDate(unix_time_in_ms)
                : _convertUnixToLocalTime(unix_time_in_ms);
        },
    },
};
</script>

<style scoped>
</style>
