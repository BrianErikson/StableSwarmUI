
function video_waveform_selected(name) {
    
}

function video_waveform_reflect_toggled() {

}

function get_video_duration_input() {
    var duration = $('#video-duration-input').val();
    if (duration === '' || isNaN(duration) || duration <= 0) {
        duration = 1;
    }
    return duration;
}

function get_frame_smoothing_input() {
    return $('input[name="frame-smoothing"]:checked').attr('id');
}

function render_video() {
    // Get the image path from the modal-image class
    var imagePath = $('#modal-video-src').attr('data-src');

    if (imagePath.startsWith('View/')) {
        imagePath = imagePath.substring('View/'.length);
        let firstSlash = imagePath.indexOf('/');
        if (firstSlash != -1) {
            imagePath = imagePath.substring(firstSlash + 1);
        }
    }

    // Disable the Render button
    $('#video-render-button').prop('disabled', true);
    // Render a spinner in the button
    $('#video-render-button').html('<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Rendering...');

    // Send a request to the server to save the video waveform, and download it
    genericRequest('ImageAsVideo', {
        'path': imagePath, 
        'frameEffect': $('input[name="frame-effect"]:checked').attr('id'), 
        'frameEffectShape': $('input[name="frame-effect-shape"]:checked').attr('id'), 
        'frameSmoothing': get_frame_smoothing_input(),
        'duration': get_video_duration_input()
    }, data => {
        // Enable the Process button
        $('#video-render-button').prop('disabled', false);
        // Reset the button text
        $('#video-render-button').html('Render');

        if (data.error) {
            console.log(data.error);
            return;
        }

        // Update the video source
        $('#modal-video-src').attr('src', 'View/local/' + data.video);
        $('#image_as_video_modal video')[0].load();
    });
}

function close_image_as_video_modal() {
    $('#image_as_video_modal').modal('hide');
}

function download_data(url, fileName, type="text/plain") {
    // Create a link element
    var a = document.createElement("a");

    // Set the href, download, and type attributes of the link
    a.href = url;
    a.download = fileName;
    a.type = type;

    // Append the link to the body
    document.body.appendChild(a);

    // Programmatically click the link
    a.click();

    // Remove the link from the body
    document.body.removeChild(a);
}
