$(function() {
    let FirstTokenHelperClass = function () {
        let _separator = undefined;

        function _getTokenLeftOfSeparator(input_string, sep) {
            let comma_index = input_string.indexOf(sep);
            return input_string.substring(0, comma_index)
        }

        return {
            init: function(separator) {
                _separator= separator;
            },
            isTokenFound: function (input_string) {
                let comma_index = input_string.indexOf(_separator);
                return comma_index !== -1;
            },
            forTokenLeftOfSeparator: function (input_string, f) {
                if (this.isTokenFound(input_string)) {
                    let token = _getTokenLeftOfSeparator(input_string, _separator)
                    f(token);
                }
            }
        }
    };

    function onBackspace(jquery_element, f, f_predicate) {
        $(jquery_element).on('keydown', function(e) {
            if(e.which === 8 && $(this).val() === "" && f_predicate()) {
                f();
                return false;
            }
        });
    }

    function showSelectedTags(selected_tags) {
        $('[data-target-name]').each(function () {
            let element_tags = $(this).data('target-name');
            if (selected_tags.length === 0 || selected_tags.every(tag => element_tags.includes(tag)))
                $(this).show();
            else
                $(this).hide();
        });
    }


    $('.tag-input-container').each(function () {
        const _tag_user_container_class = 'tag-user-container';
        const _tag_user_text_class = "tag-user-text";
        const _tag_label_template_class = 'tag-label-template';
        const _tag_remove_container_on_click_class = 'tag-remove-container-on-click';

        const _tag_list_selector = 'input.tag-list-input';
        const _tag_comma_separated_text_selector = 'input.tag-comma-separated-text';

        const _input_separator = ',';
        const _is_tag_selector = $(this).hasClass('tag-selector');
        const token_helper = FirstTokenHelperClass();

        token_helper.init(_input_separator);

        let e_tag_input_container = $(this);
        let e_tag_template = e_tag_input_container.find(`.${_tag_label_template_class}`);
        let e_tag_list = e_tag_input_container.find(_tag_list_selector);
        let _container_tag_list = [];


        function setTagListForForm() {
            e_tag_list.val(JSON.stringify(_container_tag_list));
        }

        function addNewTag(tag_name) {
            _container_tag_list.push(tag_name);
            if (_is_tag_selector)
                showSelectedTags(_container_tag_list);
            setTagListForForm()
        }

        $(e_tag_input_container)
            .find(_tag_comma_separated_text_selector)
            .each(processUserInput);

        function processUserInput() {
            const _e_input = $(this);

            function isElementBeforeInputATagUserContainer() {
                return $(_e_input).prev().hasClass(_tag_user_container_class);
            }

            function setInputText(input) {
                _e_input.val(input);
            }

            function getInputText() {
                return _e_input.val().toLowerCase();
            }

            function resetInputText() {
                setInputText("");
            }

            function removeTag(e_to_remove) {
                const e_last_tag_user_text = e_to_remove.find(`.${_tag_user_text_class}`);
                const text_last_tag = e_last_tag_user_text.text();
                _container_tag_list = _container_tag_list.filter(v => v !== text_last_tag);
                if (_is_tag_selector)
                    showSelectedTags(_container_tag_list);

                setInputText(text_last_tag);
                setTagListForForm();

                e_to_remove.remove();
            }

            function generateNewTagElement(tag_user_text) {
                let e_tag = e_tag_template.clone();
                e_tag.removeClass(_tag_label_template_class);
                e_tag.find(`.${_tag_user_text_class}`).text(tag_user_text);
                return e_tag;
            }

            function insertNewTagElement(tag_user_text) {
                generateNewTagElement(tag_user_text).insertBefore(_e_input);
            }

            onBackspace(_e_input, ()  => removeTag(_e_input.prev()), isElementBeforeInputATagUserContainer);

            function processNewTag(token) {
                insertNewTagElement(token);
                addNewTag(token);
                resetInputText();
            }

            _e_input.on('input', function () {
                const input = getInputText();
                if (input === ",")
                    setInputText("");
                else
                    token_helper.forTokenLeftOfSeparator(input, processNewTag.bind(this));
            })

            _e_input.on('keydown', function(e) {
                if(e.which === 13 && $(this).val() !== "") {
                    let input_text = getInputText();
                    if (!token_helper.isTokenFound(input_text)) {
                        processNewTag(input_text);
                        e.preventDefault();
                        return false
                    }
                }
                return true;
            });

            $(e_tag_input_container).on('click', `.${_tag_remove_container_on_click_class}`, function () {
                $(this).parents(`.${_tag_user_container_class}`).each(function () {
                    removeTag($(this));
                });
            });
        }
    })
});