<template>
    <span class='time'>
        {{ getTimeText }}
    </span>
</template>

<script>

import Timestamp from './mixins/timestamp-mixin';
export default {
    name: 'TimeLabel',
    props: {
        unixTimeStamp: {
            required: true
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
    mixins: [Timestamp],
    computed: {
        getTimeText() {
            return this._MIXIN_TIMESTAMP_getTimeText(this.unixTimeStamp, this.unixTimeStampUnits, this.showOnlyDate);
        } ,
    }
};
</script>

<style scoped>
</style>
