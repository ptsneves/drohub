<template>
<div class="box box-black">
    <div class="box-header with-border">
        <h3 class="box-title">User Permissions</h3>
    </div>
    <div class="box-body" style="padding-top:0">
        <div class="row" v-for="user in userPermissions">
            <div class="user-row">
                <div class="user-col image-col">
                    <img class="img-circle" v-bind:src="user.photo_src">
                </div>
                <div class="user-col user-data-col">
                    <div class="user-row-stacked">
                        <div class="user-name">{{user.user_name}}</div>
<!--                        <div class="user-email">({{user.user_email}})</div>-->
                    </div>
                    <div class="user-row-stacked">
                        <div class="user-type" v-bind:class="user.user_type">{{user.user_type}}</div>
                        <div v-if="user.email_confirmed" class="last-login-date">
                            Last log in:
                            <time-label
                                v-bind:unix-time-stamp="user.last_login"
                                unix-time-stamp-units="ms"
                                v-bind:show-only-date="true"
                            />
                        </div>
                        <div v-else class="pending-email-confirmation">
                            Pending Acceptance
                        </div>
                    </div>
                </div>
                <div class="user-col manage-permissions-col">
                    <a
                        v-if="agentUserEmail !== user.user_email && user.can_have_permissions_managed === true"
                        href="#"
                        v-on:click="onManagePermissions($event, user.photo_src, user.user_email, user.user_name, user.user_type)"
                        class="manage-permissions"
                    >
                        <inline-svg
                            v-bind:src="require('../../../../wwwroot/images/assets/device-window-settings.svg')"
                        />
                        Manage Permissions
                    </a>
                </div>
                <div class="user-col exclude-user-col">
                    <a
                        v-if="agentUserEmail !== user.user_email && user.can_be_excluded"
                        href="#"
                        v-on:click="onExcludeUsers($event, user.user_email, user.user_name)"
                        class="exclude-user"
                    >
                        Exclude Users
                    </a>
                </div>
            </div>
        </div>
    </div>

</div>
</template>

<script>
    import InlineSvg from 'vue-inline-svg';
    import TimeLabel from "../../components/TimeLabel";
    export default {
        name: "AccountUserPermissions",
        components: {
            InlineSvg,
            TimeLabel,
        },
        props: {
            antiForgeryToken: {
                type: String,
                required: true,
            },
            userPermissions: {
                type: Array,
                default: function () {
                    return [];
                }
            },
            agentUserEmail: {
                type: String,
                default: "",
            }
        },
        methods: {
            onExcludeUsers(event, user_email, user_name) {
                event.preventDefault();
                this.$store.commit('SET_MODAL_MODEL', {
                    type: 'EXCLUDE_USER',
                    user_email: user_email,
                    user_name: user_name,
                });
            },
            onManagePermissions(event, photo_src, user_email, user_name, user_type) {
                event.preventDefault();
                this.$store.commit('SET_MODAL_MODEL', {
                    type: 'CHANGE_PERMISSIONS',
                    photo_src: photo_src,
                    user_email: user_email,
                    user_name: user_name,
                    user_type: user_type,
                });
            },
        }
    }
</script>

<style scoped>

.pending-email-confirmation {
    margin-left: 16px;
    color: #fa730c;
}

.box.box-black {
    border-top-color: #384156;
}

.user-row {
    display: flex;
    flex-wrap: wrap;
}

.user-col {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    padding-left: 10px;
}

.user-col > img {
    max-width: 68px;
    max-height: 68px;
}

.user-row-stacked {
    display: flex;
}

.image-col {
    flex-basis: 4%;
}

.user-data-col {
    flex-direction: column;
    flex-basis: 67%;
    align-items: normal;
}

.manage-permissions-col {
    flex-basis: 16%;
}

.exclude-user-col {
    flex-basis: 12%;
}

.manage-permissions {
    color: #989898;
    font-size: 18px;
}

.manage-permissions:hover {
    font-weight: bold;
}

.exclude-user {
    color: #FF3366;
    font-size:16px;
}

.user-name {
    font-size: 24px;
}

.user-email {
    margin-left: 8px;
    font-size: 20px;
    color: grey;
}

.last-login-date {
    margin-left: 16px;
    color: grey;
}

.user-type {
    text-transform: uppercase;
    width: auto;
    height: max-content;
    border-radius: 12px;
    padding-left: 11px;
    padding-right: 11px;
    margin: 1px;
    color:white;
    display: inline-block;
    border: 0;
    font-size: 14px;
    text-align: center;
    font-weight: bold;
}

.user-type.subscriber {
    background: #1099FF;
}

.user-type.guest {
    background: #2AC940;
}

.user-type.admin {
    background: red;
}

.user-type.pilot {
    background: #8733FF;
}

.user-type.owner {
    background: #043758;
}

</style>
