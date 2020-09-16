export default {
    methods: {
        inRange(val, a, b) {
            let min = Math.min(a, b);
            let max = Math.max(a, b);
            return val >= min && val <= max;
        }
    }
}
