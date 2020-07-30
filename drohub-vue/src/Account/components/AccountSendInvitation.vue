<template>
    <div class="row">
        <div class="account-send-invitation-container">
            <div>
                <form method="post" v-bind:action="postLocation">
                    <input v-for="(email, key) in emails"
                           v-bind:key="key"
                           v-bind:value="email"
                           v-bind:name="`emails[${key}]`"
                           type="hidden" >
                    <button
                        v-bind:disabled="isDisabled"
                        class="drohub-btn-default drohub-btn-default-text"
                        :class="{'drohub-btn-default-disabled': isDisabled}"
                    >Send Invite</button>
                </form>
            </div>
            <div style="width: 100%;max-width: 800px;">
                <div class="input-box">
                    <span>
                        <i class="fa fa-envelope"></i>
                    </span>
                    <vue-tags-input
                        v-model="email"
                        v-bind:add-on-key="[13, ',', ';']"
                        v-bind:validation="validation"
                        v-bind:avoid-adding-duplicates="true"
                        v-on:tags-changed="trackEmailsChanged"
                        placeholder="Invite by email"
                    />
                </div>
            </div>
        </div>
    </div>
</template>

<script>
    import VueTagsInput from '@johmun/vue-tags-input';
    import EmailValidator from 'email-validator';
    export default {
        name: "AccountSendInvitation",
        components: {
            VueTagsInput,
        },
        props: {
            antiForgeryToken: {
                type: String,
                required: true,
            },
            postLocation: {
                type: String,
                required: true,
            },
            excludedEmailList: {
                type: String,
                default: undefined,
            }
        },
        computed: {
            isDisabled() {
                return this.emails.length === 0;
            }
        },
        data() {
            return {
                email: "",
                emails: [],
                validation: [{
                        classes: 'invalid-address',
                        rule: tag => EmailValidator.validate(tag.text) === false && tag.text.length !== 0,
                        disableAdd: true,
                    }
                ]
            }
        },
        methods: {
            trackEmailsChanged(raw_emails_array) {
                this.emails = raw_emails_array.map(e => e.text);
            },
        }
    }
</script>

<style scoped>
@import "../../assets/vue-tags-input-custom.css";
@import "../../assets/button.css";


.input-box {
    display: flex;
    margin-left: 8px;
    margin-right: 8px;
    background: white;
    padding: 6px;
}

.input-box > span {
    background-color: white;
}

.input-box > span > i.fa.fa-envelope {
    font-size: 1.1em;
    margin-top: 10px;
    margin-bottom: 10px;
}

.account-send-invitation-container {
    display: flex;
    flex-wrap: wrap;
    padding-top: 16px;
    padding-bottom: 16px;
}

.invalid-address {
    color: red;
}

</style>
