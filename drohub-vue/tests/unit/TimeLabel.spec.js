import { shallowMount } from '@vue/test-utils'
import TimeLabel from "@/components/TimeLabel.vue";

describe('TimeLabel.vue', () => {
    it('renders from seconds with date', () => {
        const wrapper = shallowMount(TimeLabel, {
            propsData: {
                unixTimeStamp: 1611493758,
                showOnlyDate: false,
                unixTimeStampUnits: 's'
            }
        });

        expect(wrapper.text()).toMatch("1/24/2021, 2:09:18 PM");
    });

    it('renders from seconds without date', () => {
        const wrapper = shallowMount(TimeLabel, {
            propsData: {
                unixTimeStamp: 1611493758,
                showOnlyDate: true,
                unixTimeStampUnits: 's'
            }
        });

        expect(wrapper.text()).toMatch("1/24/2021");
    });

    it('renders from milliseconds without date', () => {
        const wrapper = shallowMount(TimeLabel, {
            propsData: {
                unixTimeStamp: 1611493758000,
                showOnlyDate: true,
                unixTimeStampUnits: 'ms'
            }
        });

        expect(wrapper.text()).toMatch("1/24/2021");
    });

    it('renders from microseconds without date', () => {
        const wrapper = shallowMount(TimeLabel, {
            propsData: {
                unixTimeStamp: 1611493758000000,
                showOnlyDate: true,
                unixTimeStampUnits: 'us'
            }
        });

        expect(wrapper.text()).toMatch("1/24/2021");
    });

    it('renders from microseconds string without date', () => {
        const wrapper = shallowMount(TimeLabel, {
            propsData: {
                unixTimeStamp: "1611493758000000",
                showOnlyDate: true,
                unixTimeStampUnits: 'us'
            }
        });

        expect(wrapper.text()).toMatch("1/24/2021");
    });

    it('Validation fails with unknown units', () => {

        const wrapper = () => shallowMount(TimeLabel, {
            propsData: {
                unixTimeStamp: "1611493758000000",
                showOnlyDate: true,
                unixTimeStampUnits: 'DA'
            }
        });
        expect(wrapper).toThrow(Error);

    });

})
