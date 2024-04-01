
function video_waveform_selected(name) {
    
}

function video_waveform_reflect_toggled() {

}

function save_video_waveform() {
    // Get the image path from the modal-image class
    var imagePath = $('#image_as_video_modal').find('.modal-image img').attr('src');
    if (imagePath === undefined) {
        imagePath = $('#image_as_video_modal').find('.modal-image video source').attr('data-src');
    }

    // The path is in the format 'View/local/...'. We need to convert it to a relative path for the server
    imagePath = imagePath.replace('View/local/', '');

    var duration = $('#video-duration-input').val();
    if (isNaN(duration)) {
        duration = 10;
    }

    // Send a request to the server to save the video waveform, and download it
    genericRequest('ImageAsVideo', {
        'path': imagePath, 
        'frameEffect': $('input[name="frame-effect"]:checked').attr('id'), 
        'frameEffectShape': $('input[name="frame-effect-shape"]:checked').attr('id'), 
        'duration': duration,
    }, data => {
        if (data.error) {
            // TODO: What is the standard way to handle errors in this project?
            console.log(data.error);
            return;
        }

        var lastSlash = Math.max(data.video.lastIndexOf('/'), data.video.lastIndexOf('\\'));
        var fileName = data.video.substring(lastSlash + 1, data.video.lastIndexOf('.'));

        var relPath = 'View/local/' + data.video;

        // replace the modal-image with the video
        $('#image_as_video_modal .modal-image').html(`<video controls autoplay loop class="img-fluid"><source class="modal-image" data-src="${imagePath}" src="${relPath}" type="video/mp4"></video>`);

        // convert the relative path to an absolute path
        var url = window.location.href;
        var absPath = url.substring(0, url.lastIndexOf('/')) + '/' + relPath;

        // TODO fix this
        //$('#image_as_video_modal').modal('hide');
        //download_data(absPath, fileName, 'video/mp4');
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
