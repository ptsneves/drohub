$(function() {
   $('.toggle').each(function () {
      function toggle(e) {
         let element = $(e);
         let toggle_state = element.data('toggle-state');
         let toggle_id = element.data('toggle-id');

         $(`.show-on-toggle-true[data-toggle-id=${toggle_id}]`).each(function () {
            toggle_state === true ? $(this).show() : $(this).hide();
         });
         $(`.show-on-toggle-false[data-toggle-id=${toggle_id}]`).each(function ( ) {
            toggle_state === true ? $(this).hide() : $(this).show();
         });
         element.data('toggle-state', toggle_state !== true)
      }

      $(this).on("click", function(event) {
         toggle($(this));
      })

      toggle($(this));
   });

   $('#deleteMediaModal').on('show.bs.modal', function(e) {
      let media_path = $(e.relatedTarget).data('media-path');
      let media_description = $(e.relatedTarget).data('media-description');
      $(e.currentTarget).find('input[name="media_path"]').val(media_path);
      $(e.currentTarget).find('.append-file-path-text').each(function () {
         let template_text = $(this).data('text-template');
         $(this).text(`${template_text} ${media_description}`);
      });
   });

   function initICheckForTagLabels() {
      $('input.icheck-label').each(function () {
         let self = $(this),
             label = self.next(),
             label_text = label.text();

         label.remove();
         self.iCheck({
            checkboxClass: 'icheckbox_tag-label',
            radioClass: 'iradio_line-blue',
            insert: '<a href="#"><i class="fa fa-times"></i></a>' + label_text
         });
      });

      $('input.icheck').iCheck({
         checkboxClass: 'icheckbox_square-blue',
         radioClass: 'iradio_square-blue',
         increaseArea: '20%' /* optional */
      });
   }
   initICheckForTagLabels();


   function manageTagSelection() {
      function showSelectedTags(selected_tags) {
         $('[data-target-name]').each(function () {
            let element_tags = $(this).data('target-name');
            if (selected_tags.length === 0 || selected_tags.toArray().every(tag => element_tags.includes(tag)))
               $(this).show();
               // let cur_text = $('input.tag-search-box').text();

            else
               $(this).hide();
         });
      }

      $('input.tag-checkbox').on('click', function () {
         let selected_tags = [];
         $('input.tag-checkbox:checked').each(function () {
            selected_tags.push($(this).data('tag-name'));
         });
         showSelectedTags(selected_tags);
      });

      $('a.delete-tag-btn').on('click', function (e) {
         const delete_tag_url = $(this).prop('href');
         const tag_name = $(this).data('tag-name');
         const media_id = $(this).data('media-id');
         e.preventDefault();

         $.post(delete_tag_url, { tag_name: tag_name, media_id: media_id })
             .done(function( data ) {
                $(this).parent().remove();
             }.bind(this));
      });
   }
   manageTagSelection();

   $('#addTagModal').on('show.bs.modal', function(e) {
      let media_path = $(e.relatedTarget).data('media-path');
      let e_video_curtime = $(e.relatedTarget).find('.video-player-text.video-player-curtime');
      let curtime_string = e_video_curtime.text();
      $(e.currentTarget).find('input.tag-timestamp-value').val(curtime_string);
      $(e.currentTarget).find('.tag-timestamp-text').text(curtime_string);
      $(e.currentTarget).find('input.tag-media-path-value').val(media_path);
   });
});
