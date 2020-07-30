<template>
    <div v-if="isActiveModal">
        <div class="modal-mask" style="top:30vh;">
            <div class="modal-dialog">
                <div class="modal-content modal-content-extra">
                    <form method="post" v-bind:action="postLocation">
                        <div class="modal-body">
                            <div class="modal-body-title">
                                Are you sure you wish to
                                <br/>
                                Exclude the user <i>{{userName}}?</i>
                            </div>
                        </div>
                        <input type="hidden" name="user_email" v-bind:value="userEmail"/>
                        <div class="modal-footer modal-footer-extra">
                            <button
                                class="drohub-btn-default drohub-btn-default-text"
                                style="background: grey !important"
                                v-on:click="hideModal"
                            >Cancel</button>
                            <button
                                class="drohub-btn-danger drohub-btn-default-text"
                                type="submit"
                            >Exclude</button>
                        </div>
                    </form>
                </div>
            </div>
        </div>
        <div class="modal-overlay"></div>
    </div>
</template>

<script>
    export default {
        name: "AccountExcludeUserModal",
        data() {
            return {
                MODAL_TYPE: 'EXCLUDE_USER',
                picked: "",
            }
        },
        props: {
            postLocation: {
                type: String,
                required: true,
            },
            antiForgeryToken: {
                type: String,
                required: true,
            },
        },
        computed: {
            userEmail() {
                return this.$store.state.modal_model.user_email;
            },
            userName() {
                return this.$store.state.modal_model.user_name;
            },
            isActiveModal() {
                return this.$store.state.modal_model.type === this.MODAL_TYPE;
            },
        },
        methods: {
            hideModal() {
                return this.$store.commit('SET_MODAL_MODEL', {type: 'INACTIVE'});
            },
        }
    }
</script>

<style scoped>
.modal-body-title {
    text-align: center;
    font-size: 22px;
}
</style>
